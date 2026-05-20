using MeetingSim.Core.Personas;

namespace MeetingSim.Api.Contracts;

public sealed record PersonasResponse(
    IReadOnlyList<Persona> Roster,
    IReadOnlyList<Persona> Crowd,
    int CrowdSize);
