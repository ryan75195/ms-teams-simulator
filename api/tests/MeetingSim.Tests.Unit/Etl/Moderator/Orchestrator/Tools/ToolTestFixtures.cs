using System.Text.Json;
using System.Text.Json.Nodes;
using MeetingSim.Core.Personas;
using MeetingSim.Etl.Moderator.Interfaces;
using MeetingSim.Etl.Moderator.Orchestrator;
using MeetingSim.Etl.Voice.Interfaces;

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

internal sealed class FakePersonaVoiceService : IPersonaVoiceService
{
    public string? CannedResponse { get; set; } = "default line";

    public List<string> RequestedPersonas { get; } = [];

    public Task<string> GenerateLine(
        string personaId,
        string presenterLine,
        IReadOnlyList<string> recentChunks,
        IReadOnlyList<string> personaPreviousLines,
        CancellationToken cancellationToken = default)
    {
        RequestedPersonas.Add(personaId);
        return Task.FromResult(CannedResponse ?? string.Empty);
    }
}
