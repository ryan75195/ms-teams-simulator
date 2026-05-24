using MeetingSim.Core.Personas;

namespace MeetingSim.Etl.Moderator.Orchestrator;

public sealed record ModeratorContext(
    Guid SessionId,
    string PresenterLine,
    IReadOnlyList<string> RecentChunks,
    string? ActiveResponderId,
    IReadOnlyCollection<string> HandsUp,
    IReadOnlyList<string> RecentSpeakers,
    IReadOnlyList<Persona> Roster,
    IReadOnlyDictionary<string, IReadOnlyList<string>> PersonaPreviousLines,
    string? CurrentSlide = null,
    string Mode = "complete",
    IReadOnlyList<string>? RecentDecisions = null);
