namespace MeetingSim.Etl.Moderator;

internal sealed record ModeratorDecision(
    string Action,
    string? PersonaId,
    string? Text,
    bool? Raised,
    string Reasoning);
