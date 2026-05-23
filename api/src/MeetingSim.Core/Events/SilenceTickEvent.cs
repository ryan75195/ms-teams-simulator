namespace MeetingSim.Core.Events;

public sealed record SilenceTickEvent(
    long Id,
    DateTimeOffset Ts,
    int Seconds) : MeetingEvent(Id, Ts);
