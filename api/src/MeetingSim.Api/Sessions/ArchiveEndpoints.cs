using System.IO.Compression;
using System.Text;
using MeetingSim.Api.Sessions.Interfaces;

namespace MeetingSim.Api.Sessions;

public static class ArchiveEndpoints
{
    public static IEndpointRouteBuilder MapArchiveEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/sessions/archived", ListArchived);
        app.MapGet("/sessions/{id:guid}/transcript.md", GetTranscript);
        app.MapGet("/sessions/{id:guid}/archive.zip", GetArchive);
        return app;
    }

    private static async Task<IResult> ListArchived(ISessionArchive archive, CancellationToken cancellationToken)
    {
        var ids = archive.ListArchivedSessions();
        var manifests = new List<SessionManifest>();
        foreach (var id in ids)
        {
            var manifest = await archive.LoadManifest(id, cancellationToken).ConfigureAwait(false);
            if (manifest is not null)
            {
                manifests.Add(manifest);
            }
        }
        manifests.Sort((a, b) => b.StartedAt.CompareTo(a.StartedAt));
        return Results.Ok(manifests);
    }

    private static async Task<IResult> GetTranscript(
        Guid id,
        ISessionArchive archive,
        CancellationToken cancellationToken)
    {
        var manifest = await archive.LoadManifest(id, cancellationToken).ConfigureAwait(false);
        if (manifest is null)
        {
            return Results.NotFound();
        }
        var lines = new List<string>();
        await foreach (var line in archive.ReadEventLines(id, cancellationToken).ConfigureAwait(false))
        {
            lines.Add(line);
        }
        var markdown = TranscriptRenderer.Render(manifest, lines);
        return Results.Text(markdown, "text/markdown; charset=utf-8");
    }

    private static IResult GetArchive(Guid id, ISessionArchive archive)
    {
        var dir = archive.GetSessionDirectory(id);
        if (!Directory.Exists(dir))
        {
            return Results.NotFound();
        }
        var stream = BuildZipStream(dir);
        return Results.File(stream, "application/zip", $"session-{id:N}.zip");
    }

    private static Stream BuildZipStream(string sessionDirectory)
    {
        var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true, Encoding.UTF8))
        {
            foreach (var path in Directory.EnumerateFiles(sessionDirectory, "*", SearchOption.AllDirectories))
            {
                var entryName = Path.GetRelativePath(sessionDirectory, path).Replace('\\', '/');
                var entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
                using var entryStream = entry.Open();
                using var fileStream = File.OpenRead(path);
                fileStream.CopyTo(entryStream);
            }
        }
        buffer.Position = 0;
        return buffer;
    }
}
