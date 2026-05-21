using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using OpenAI.Chat;

namespace MeetingSim.Etl.Moderator;

internal static class ModeratorRunner
{
    private const string DefaultModelName = "gpt-4o-mini";
    private const int RecentSpeakersWindow = 2;
    private const int RecentChunksWindow = 6;
    private const string TranscriptKind = "transcript";

    public static async Task<int> Run(string[] args)
    {
        var parsed = ParseArgs(args);
        if (parsed is null)
        {
            return 1;
        }

        using var http = new HttpClient { BaseAddress = parsed.ApiUri };

        var personas = await FetchPersonas(http, parsed.SessionId).ConfigureAwait(false);
        if (personas is null || personas.Roster.Count == 0)
        {
            await Console.Error.WriteLineAsync($"Failed to fetch personas for session {parsed.SessionId}.");
            return 1;
        }

        var moderator = new ModeratorService(
            new ChatClient(model: parsed.ModelName, apiKey: parsed.ApiKey),
            personas.Roster);
        var poster = new ApiEventClient(http, parsed.SessionId);

        return await RunConnection(parsed, personas, moderator, poster).ConfigureAwait(false);
    }

    private static async Task<int> RunConnection(
        ParsedArgs parsed,
        PersonasPayload personas,
        ModeratorService moderator,
        ApiEventClient poster)
    {
        var transcriptBuffer = new List<string>();
        var recentSpeakers = new Queue<string>();
        var hubUrl = new Uri(parsed.ApiUri, "/hubs/session");

        await using var connection = BuildConnection(hubUrl);
        connection.On<JsonElement>("event", async evt =>
            await SafeHandleEvent(evt, transcriptBuffer, recentSpeakers, moderator, poster).ConfigureAwait(false));

        Console.WriteLine($"Moderator model: {parsed.ModelName}");
        Console.WriteLine($"Connecting to {hubUrl} for session {parsed.SessionId}…");
        await connection.StartAsync().ConfigureAwait(false);
        await connection.InvokeAsync("JoinSession", parsed.SessionId).ConfigureAwait(false);
        Console.WriteLine($"Live with {personas.Roster.Count} personas. Listening for transcript events. Ctrl+C to stop.");

        await WaitForCancelKey().ConfigureAwait(false);
        await connection.StopAsync().ConfigureAwait(false);
        return 0;
    }

    private static ParsedArgs? ParseArgs(string[] args)
    {
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Console.Error.WriteLine("OPENAI_API_KEY is not set. Set it in your environment and re-run.");
            return null;
        }

        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: moderator <api-url> <session-id> [model]");
            return null;
        }

        if (!Uri.TryCreate(args[0], UriKind.Absolute, out var apiUri))
        {
            Console.Error.WriteLine($"Invalid api-url: {args[0]}");
            return null;
        }

        if (!Guid.TryParse(args[1], out var sessionId))
        {
            Console.Error.WriteLine($"Invalid session-id: {args[1]}");
            return null;
        }

        var modelName = args.Length > 2 ? args[2] : DefaultModelName;
        return new ParsedArgs(apiKey, apiUri, sessionId, modelName);
    }

    private static async Task<PersonasPayload?> FetchPersonas(HttpClient http, Guid sessionId)
    {
        var url = $"/sessions/{sessionId}/personas?count=20";
        return await http.GetFromJsonAsync<PersonasPayload>(url).ConfigureAwait(false);
    }

    private static HubConnection BuildConnection(Uri hubUrl)
    {
        return new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.Transports = HttpTransportType.WebSockets | HttpTransportType.LongPolling;
            })
            .WithAutomaticReconnect()
            .Build();
    }

    private static async Task WaitForCancelKey()
    {
        var cancellation = new TaskCompletionSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cancellation.TrySetResult();
        };
        await cancellation.Task.ConfigureAwait(false);
    }

    private static async Task SafeHandleEvent(
        JsonElement evt,
        List<string> transcriptBuffer,
        Queue<string> recentSpeakers,
        ModeratorService moderator,
        ApiEventClient poster)
    {
        try
        {
            await HandleEvent(evt, transcriptBuffer, recentSpeakers, moderator, poster).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            await Console.Error.WriteLineAsync($"Event POST failed: {ex.Message}").ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            await Console.Error.WriteLineAsync($"Event JSON malformed: {ex.Message}").ConfigureAwait(false);
        }
    }

    private static async Task HandleEvent(
        JsonElement evt,
        List<string> transcriptBuffer,
        Queue<string> recentSpeakers,
        ModeratorService moderator,
        ApiEventClient poster)
    {
        if (!IsTranscript(evt, out var text))
        {
            return;
        }

        var seen = transcriptBuffer.ToList();
        AppendToBuffer(transcriptBuffer, text);

        Console.WriteLine();
        Console.WriteLine($"[transcript] {text}");

        var decision = await moderator
            .DecideAsync(text, seen, recentSpeakers.ToList())
            .ConfigureAwait(false);
        PrintDecision(decision);

        if (string.Equals(decision.Action, "none", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await poster.Post(decision).ConfigureAwait(false);
        AdvanceSpeakers(decision, recentSpeakers);
    }

    private static bool IsTranscript(JsonElement evt, out string text)
    {
        text = string.Empty;
        if (!evt.TryGetProperty("kind", out var kindEl) || kindEl.GetString() != TranscriptKind)
        {
            return false;
        }
        if (!evt.TryGetProperty("text", out var textEl))
        {
            return false;
        }
        var payload = textEl.GetString();
        if (string.IsNullOrWhiteSpace(payload))
        {
            return false;
        }
        text = payload;
        return true;
    }

    private static void AppendToBuffer(List<string> buffer, string text)
    {
        buffer.Add(text);
        while (buffer.Count > RecentChunksWindow + 2)
        {
            buffer.RemoveAt(0);
        }
    }

    private static void PrintDecision(ModeratorDecision decision)
    {
        Console.WriteLine($"  decision: {decision.Action} {decision.PersonaId ?? "-"} — {decision.Reasoning}");
        if (!string.IsNullOrEmpty(decision.Text))
        {
            Console.WriteLine($"  text    : {decision.Text}");
        }
    }

    private static void AdvanceSpeakers(ModeratorDecision decision, Queue<string> recentSpeakers)
    {
        if (decision.PersonaId is not { Length: > 0 } speaker)
        {
            return;
        }

        recentSpeakers.Enqueue(speaker);
        while (recentSpeakers.Count > RecentSpeakersWindow)
        {
            recentSpeakers.Dequeue();
        }
    }

    private sealed record ParsedArgs(string ApiKey, Uri ApiUri, Guid SessionId, string ModelName);
}
