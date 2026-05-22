using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using MeetingSim.Api.Sessions.Interfaces;
using MeetingSim.Core.Events;

namespace MeetingSim.Api.Sessions;

internal sealed class FileSystemSessionArchive : ISessionArchive
{
    private static readonly JsonSerializerOptions LineOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static readonly JsonSerializerOptions ManifestOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string _rootDirectory;
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _locks = new();

    public FileSystemSessionArchive(IConfiguration configuration)
    {
        var configured = configuration["SessionArchive:Root"];
        _rootDirectory = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(Environment.CurrentDirectory, "sessions")
            : Path.GetFullPath(configured);
        Directory.CreateDirectory(_rootDirectory);
    }

    public async Task SaveManifest(SessionManifest manifest, CancellationToken cancellationToken = default)
    {
        var dir = SessionDirectory(manifest.Id);
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "manifest.json");
        var json = JsonSerializer.Serialize(manifest, ManifestOptions);
        await File.WriteAllTextAsync(path, json, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
    }

    public async Task<SessionManifest?> LoadManifest(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(SessionDirectory(sessionId), "manifest.json");
        if (!File.Exists(path))
        {
            return null;
        }
        try
        {
            await using var stream = File.OpenRead(path);
            return await JsonSerializer
                .DeserializeAsync<SessionManifest>(stream, ManifestOptions, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public async Task AppendEvent(Guid sessionId, MeetingEvent evt, CancellationToken cancellationToken = default)
    {
        var dir = SessionDirectory(sessionId);
        Directory.CreateDirectory(dir);
        var line = JsonSerializer.Serialize<MeetingEvent>(evt, LineOptions);
        await AppendLine(sessionId, Path.Combine(dir, "events.jsonl"), line, cancellationToken).ConfigureAwait(false);
    }

    public async Task AppendDecision(Guid sessionId, JsonObject decision, CancellationToken cancellationToken = default)
    {
        var dir = SessionDirectory(sessionId);
        Directory.CreateDirectory(dir);
        var line = decision.ToJsonString();
        await AppendLine(sessionId, Path.Combine(dir, "decisions.jsonl"), line, cancellationToken).ConfigureAwait(false);
    }

    public async Task WriteAudio(
        Guid sessionId,
        long eventId,
        ReadOnlyMemory<byte> bytes,
        CancellationToken cancellationToken = default)
    {
        var dir = Path.Combine(SessionDirectory(sessionId), "audio");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"{eventId}.mp3");
        await File.WriteAllBytesAsync(path, bytes.ToArray(), cancellationToken).ConfigureAwait(false);
    }

    public Task<Stream?> OpenAudio(Guid sessionId, long eventId, CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(SessionDirectory(sessionId), "audio", $"{eventId}.mp3");
        if (!File.Exists(path))
        {
            return Task.FromResult<Stream?>(null);
        }
        Stream stream = File.OpenRead(path);
        return Task.FromResult<Stream?>(stream);
    }

    public IReadOnlyList<Guid> ListArchivedSessions()
    {
        if (!Directory.Exists(_rootDirectory))
        {
            return [];
        }
        var ids = new List<Guid>();
        foreach (var dir in Directory.EnumerateDirectories(_rootDirectory))
        {
            if (Guid.TryParse(Path.GetFileName(dir), out var id))
            {
                ids.Add(id);
            }
        }
        return ids;
    }

    public async IAsyncEnumerable<string> ReadEventLines(
        Guid sessionId,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(SessionDirectory(sessionId), "events.jsonl");
        if (!File.Exists(path))
        {
            yield break;
        }
        using var reader = new StreamReader(path, Encoding.UTF8);
        while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
        {
            yield return line;
        }
    }

    public string GetSessionDirectory(Guid sessionId) => SessionDirectory(sessionId);

    private async Task AppendLine(Guid sessionId, string path, string line, CancellationToken cancellationToken)
    {
        var sem = _locks.GetOrAdd(sessionId, _ => new SemaphoreSlim(1, 1));
        await sem.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await File.AppendAllTextAsync(path, line + "\n", Encoding.UTF8, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            sem.Release();
        }
    }

    private string SessionDirectory(Guid sessionId) =>
        Path.Combine(_rootDirectory, sessionId.ToString("D"));
}
