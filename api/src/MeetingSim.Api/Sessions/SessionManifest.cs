using MeetingSim.Core.Personas;
using MeetingSim.Core.Sessions;

namespace MeetingSim.Api.Sessions;

public sealed record SessionManifest(
    Guid Id,
    string Title,
    int AudienceSize,
    int Seed,
    SessionSettings Settings,
    IReadOnlyList<Persona> Roster,
    DateTimeOffset StartedAt,
    DateTimeOffset? LastEventAt,
    DateTimeOffset? EndedAt);
