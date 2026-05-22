using System.Net.Http.Json;
using System.Text.Json;
using MeetingSim.Core.Personas;
using MeetingSim.Etl.Moderator.Interfaces;
using MeetingSim.Etl.Moderator.Orchestrator;
using MeetingSim.Etl.Moderator.Orchestrator.Interfaces;
using MeetingSim.Etl.Moderator.Orchestrator.Tools;
using MeetingSim.Etl.Voice;
using MeetingSim.Etl.Voice.Interfaces;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using OpenAI.Chat;

namespace MeetingSim.Etl.Moderator;

internal static class ModeratorRunner
{
    private const string DefaultModelName = "gpt-4o-mini";
    private const int RecentSpeakersWindow = 2;
    private const int RecentChunksWindow = 6;
    private const int PreviousLinesPerPersona = 3;
    private const string TranscriptKind = "transcript";
    private const string SpeakKind = "speak";
    private const string HandRaiseKind = "hand-raise";
    private const string SlideUpdateKind = "slide-update";
    private static readonly TimeSpan ActiveResponderTtl = TimeSpan.FromSeconds(20);

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

        var chat = new ChatClient(model: parsed.ModelName, apiKey: parsed.ApiKey);
        var apiClient = new ApiEventClient(http, parsed.SessionId);
        IEventPoster poster = apiClient;
        IDecisionPoster decisionPoster = apiClient;
        IPersonaVoiceService voice = new OpenAIPersonaVoiceService(chat, personas.Roster);

        var registry = BuildRegistry(personas.Roster, poster, voice);
        var orchestrator = new ModeratorOrchestrator(chat, registry, decisionPoster, personas.Roster);

        return await RunConnection(parsed, personas, orchestrator).ConfigureAwait(false);
    }

    private static ModeratorToolRegistry BuildRegistry(
        IReadOnlyList<Persona> roster,
        IEventPoster poster,
        IPersonaVoiceService voice)
    {
        var tools = new IModeratorTool[]
        {
            new StayQuietTool(),
            new RaiseHandTool(roster, poster),
            new ReactTool(roster, poster),
            new SendChatTool(roster, poster, voice),
            new CastSpeakTool(roster, poster, voice),
        };
        return new ModeratorToolRegistry(tools);
    }

    private static async Task<int> RunConnection(
        ParsedArgs parsed,
        PersonasPayload personas,
        ModeratorOrchestrator orchestrator)
    {
        var state = new ModeratorState();
        var hubUrl = new Uri(parsed.ApiUri, "/hubs/session");

        await using var connection = BuildConnection(hubUrl);
        connection.On<JsonElement>("event", async evt =>
            await SafeHandleEvent(evt, state, orchestrator, personas.Roster).ConfigureAwait(false));

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
        ModeratorState state,
        ModeratorOrchestrator orchestrator,
        IReadOnlyList<Persona> roster)
    {
        try
        {
            await HandleEvent(evt, state, orchestrator, roster).ConfigureAwait(false);
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
        ModeratorState state,
        ModeratorOrchestrator orchestrator,
        IReadOnlyList<Persona> roster)
    {
        var kind = ReadKind(evt);
        if (kind == TranscriptKind)
        {
            await HandleTranscript(evt, state, orchestrator, roster).ConfigureAwait(false);
        }
        else if (kind == SpeakKind)
        {
            HandleSpeakBroadcast(evt, state);
        }
        else if (kind == HandRaiseKind)
        {
            HandleHandRaise(evt, state);
        }
        else if (kind == SlideUpdateKind)
        {
            HandleSlideUpdate(evt, state);
        }
    }

    private static void HandleSlideUpdate(JsonElement evt, ModeratorState state)
    {
        if (!evt.TryGetProperty("text", out var textEl))
        {
            return;
        }
        state.CurrentSlide = textEl.GetString();
    }

    private static async Task HandleTranscript(
        JsonElement evt,
        ModeratorState state,
        ModeratorOrchestrator orchestrator,
        IReadOnlyList<Persona> roster)
    {
        if (!TryReadTranscriptText(evt, out var text))
        {
            return;
        }

        state.ExpireActiveResponder(DateTimeOffset.UtcNow);
        var seen = state.TranscriptBuffer.ToList();
        AppendToBuffer(state.TranscriptBuffer, text);
        var calledOut = CalloutDetector.Detect(text, roster);

        Console.WriteLine();
        Console.WriteLine($"[transcript] {text}");
        Console.WriteLine(
            $"  state    : active={state.ActiveResponderId ?? "-"} " +
            $"hands={(state.HandsUp.Count == 0 ? "-" : string.Join(",", state.HandsUp))} " +
            $"recent={(state.RecentSpeakers.Count == 0 ? "-" : string.Join(",", state.RecentSpeakers))} " +
            $"callout={calledOut ?? "-"}");

        var context = new ModeratorContext(
            SessionId: Guid.Empty,
            PresenterLine: text,
            RecentChunks: seen,
            ActiveResponderId: state.ActiveResponderId,
            HandsUp: state.HandsUp,
            RecentSpeakers: state.RecentSpeakers.ToList(),
            Roster: roster,
            PersonaPreviousLines: state.PersonaPreviousLines,
            CalledOutPersonaId: calledOut,
            CurrentSlide: state.CurrentSlide);

        await orchestrator.Decide(context).ConfigureAwait(false);
    }

    private static void HandleSpeakBroadcast(JsonElement evt, ModeratorState state)
    {
        if (!TryReadPersonaId(evt, out var personaId))
        {
            return;
        }
        state.ActiveResponderId = personaId;
        state.ActiveResponderExpiresAt = DateTimeOffset.UtcNow + ActiveResponderTtl;
        state.AdvanceRecentSpeakers(personaId, RecentSpeakersWindow);
        if (evt.TryGetProperty("text", out var textEl) && textEl.GetString() is { Length: > 0 } text)
        {
            state.AppendPersonaLine(personaId, text, PreviousLinesPerPersona);
        }
    }

    private static void HandleHandRaise(JsonElement evt, ModeratorState state)
    {
        if (!TryReadPersonaId(evt, out var personaId))
        {
            return;
        }
        var raised = evt.TryGetProperty("raised", out var raisedEl) && raisedEl.GetBoolean();
        if (raised)
        {
            state.HandsUp.Add(personaId);
        }
        else
        {
            state.HandsUp.Remove(personaId);
        }
    }

    private static string? ReadKind(JsonElement evt)
        => evt.TryGetProperty("kind", out var kindEl) ? kindEl.GetString() : null;

    private static bool TryReadTranscriptText(JsonElement evt, out string text)
    {
        text = string.Empty;
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

    private static bool TryReadPersonaId(JsonElement evt, out string personaId)
    {
        personaId = string.Empty;
        if (!evt.TryGetProperty("personaId", out var idEl))
        {
            return false;
        }
        var value = idEl.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }
        personaId = value;
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

    private sealed record ParsedArgs(string ApiKey, Uri ApiUri, Guid SessionId, string ModelName);

    private sealed class ModeratorState
    {
        private readonly Dictionary<string, List<string>> _personaUtterances = new(StringComparer.Ordinal);
        private readonly Dictionary<string, IReadOnlyList<string>> _personaUtterancesView = new(StringComparer.Ordinal);

        public List<string> TranscriptBuffer { get; } = [];

        public Queue<string> RecentSpeakers { get; } = new();

        public HashSet<string> HandsUp { get; } = new(StringComparer.Ordinal);

        public string? ActiveResponderId { get; set; }

        public string? CurrentSlide { get; set; }

        public DateTimeOffset? ActiveResponderExpiresAt { get; set; }

        public IReadOnlyDictionary<string, IReadOnlyList<string>> PersonaPreviousLines => _personaUtterancesView;

        public void ExpireActiveResponder(DateTimeOffset now)
        {
            if (ActiveResponderExpiresAt is { } expires && expires <= now)
            {
                ActiveResponderId = null;
                ActiveResponderExpiresAt = null;
            }
        }

        public void AdvanceRecentSpeakers(string personaId, int window)
        {
            RecentSpeakers.Enqueue(personaId);
            while (RecentSpeakers.Count > window)
            {
                RecentSpeakers.Dequeue();
            }
        }

        public void AppendPersonaLine(string personaId, string line, int maxLinesPerPersona)
        {
            if (!_personaUtterances.TryGetValue(personaId, out var list))
            {
                list = [];
                _personaUtterances[personaId] = list;
                _personaUtterancesView[personaId] = list;
            }
            list.Add(line);
            while (list.Count > maxLinesPerPersona)
            {
                list.RemoveAt(0);
            }
        }
    }
}
