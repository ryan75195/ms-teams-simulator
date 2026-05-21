using MeetingSim.Core.Personas;

namespace MeetingSim.Etl.Moderator;

internal sealed record PersonasPayload(
    IReadOnlyList<Persona> Roster,
    IReadOnlyList<Persona> Crowd,
    int CrowdSize);
