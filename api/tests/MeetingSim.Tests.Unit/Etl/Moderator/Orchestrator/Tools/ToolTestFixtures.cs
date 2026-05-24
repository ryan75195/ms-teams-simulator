using System.Text.Json;
using System.Text.Json.Nodes;
using MeetingSim.Core.Personas;
using MeetingSim.Etl.Chat.Interfaces;
using MeetingSim.Etl.Moderator.Interfaces;
using MeetingSim.Etl.Moderator.Orchestrator;
using MeetingSim.Etl.Voice.Interfaces;
using OpenAI.Chat;

namespace MeetingSim.Tests.Unit.Etl.Moderator.Orchestrator.Tools;

internal static class ToolTestFixtures
{
    public static IReadOnlyList<Persona> NewRoster() =>
    [
        new("you", "Ryan Khan", "#7A4F8B", Archetype.User, IsRoster: true),
        new("anuj", "Anuj Kapoor", "#3F628E", Archetype.Skeptic, IsRoster: true),
        new("serena", "Serena Davis", "#6E5BC5", Archetype.Curious, IsRoster: true),
        new("kayo", "Kayo Miwa", "#3C7A6B", Archetype.Cheerleader, IsRoster: true),
    ];

    public static ModeratorContext NewContext(
        string presenterLine = "Hello.",
        string? activeResponder = null,
        IReadOnlyCollection<string>? handsUp = null,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? personaPreviousLines = null)
        => new(
            SessionId: Guid.NewGuid(),
            PresenterLine: presenterLine,
            RecentChunks: [],
            ActiveResponderId: activeResponder,
            HandsUp: handsUp ?? new HashSet<string>(),
            RecentSpeakers: [],
            Roster: NewRoster(),
            PersonaPreviousLines: personaPreviousLines ?? new Dictionary<string, IReadOnlyList<string>>());

    public static JsonElement Args(string json)
        => JsonDocument.Parse(json).RootElement;
}

internal sealed class FakeEventPoster : IEventPoster
{
    public List<JsonObject> Posted { get; } = [];

    public Task PostEvent(JsonObject body, CancellationToken cancellationToken = default)
    {
        Posted.Add((JsonObject)body.DeepClone());
        return Task.CompletedTask;
    }
}

internal sealed class FakeModeratorStateMutator : IModeratorStateMutator
{
    public List<string?> Calls { get; } = [];

    public string? Current { get; private set; }

    public void SetActiveResponder(string? personaId)
    {
        Calls.Add(personaId);
        Current = personaId;
    }
}

internal sealed class FakeChatCompleter : IChatCompleter
{
    private readonly Queue<ChatCompletion> _responses = new();
    public List<List<ChatMessage>> Calls { get; } = [];

    public void Enqueue(ChatCompletion completion) => _responses.Enqueue(completion);

    public Task<ChatCompletion> Complete(
        IEnumerable<ChatMessage> messages,
        ChatCompletionOptions? options,
        CancellationToken cancellationToken)
    {
        Calls.Add(messages.ToList());
        if (_responses.Count == 0)
        {
            throw new InvalidOperationException("FakeChatCompleter has no canned responses left.");
        }
        return Task.FromResult(_responses.Dequeue());
    }
}

internal sealed class FakeDecisionPoster : IDecisionPoster
{
    public List<JsonObject> Posted { get; } = [];

    public Task PostDecision(JsonObject decision, CancellationToken cancellationToken = default)
    {
        Posted.Add((JsonObject)decision.DeepClone());
        return Task.CompletedTask;
    }
}

#pragma warning disable OPENAI001
internal static class FakeCompletionFactory
{
    public static ChatCompletion WithToolCalls(params (string Name, string Args)[] calls)
    {
        var toolCalls = calls
            .Select(c => ChatToolCall.CreateFunctionToolCall(
                id: Guid.NewGuid().ToString(),
                functionName: c.Name,
                functionArguments: BinaryData.FromString(c.Args)))
            .ToList();
        return OpenAIChatModelFactory.ChatCompletion(toolCalls: toolCalls);
    }

    public static ChatCompletion WithText(string text)
    {
        var part = ChatMessageContentPart.CreateTextPart(text);
        return OpenAIChatModelFactory.ChatCompletion(content: new ChatMessageContent(part));
    }
}
#pragma warning restore OPENAI001

internal sealed class FakePersonaVoiceService : IPersonaVoiceService
{
    public string? CannedResponse { get; set; } = "default line";

    public List<string> RequestedPersonas { get; } = [];

    public List<string?> RequestedSlides { get; } = [];

    public Task<string> GenerateLine(
        string personaId,
        string presenterLine,
        IReadOnlyList<string> recentChunks,
        IReadOnlyList<string> personaPreviousLines,
        string? currentSlide = null,
        CancellationToken cancellationToken = default)
    {
        RequestedPersonas.Add(personaId);
        RequestedSlides.Add(currentSlide);
        return Task.FromResult(CannedResponse ?? string.Empty);
    }
}
