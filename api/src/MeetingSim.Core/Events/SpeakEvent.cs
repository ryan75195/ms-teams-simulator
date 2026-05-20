namespace MeetingSim.Core.Events;

public sealed record SpeakEvent(
    long Id,
    DateTimeOffset Ts,
    string PersonaId,
    int DurationMs) : MeetingEvent(Id, Ts);
