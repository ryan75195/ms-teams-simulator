namespace MeetingSim.Core.Events;

public sealed record SlideUpdateEvent(
    long Id,
    DateTimeOffset Ts,
    string Text) : MeetingEvent(Id, Ts);
