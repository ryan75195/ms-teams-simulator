namespace MeetingSim.Core.Personas;

public sealed record Persona(
    string Id,
    string Name,
    string Color,
    Archetype Archetype,
    bool IsRoster,
    string? Bio = null);
