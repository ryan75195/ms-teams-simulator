namespace MeetingSim.Core.Sessions;

public sealed record Session(
    Guid Id,
    string Title,
    int AudienceSize,
    int Seed,
    DateTimeOffset StartedAt,
    SessionSettings Settings);
