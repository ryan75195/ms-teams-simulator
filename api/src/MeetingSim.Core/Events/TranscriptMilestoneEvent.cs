namespace MeetingSim.Core.Events;

public sealed record TranscriptMilestoneEvent(
    long Id,
    DateTimeOffset Ts,
    string Text) : MeetingEvent(Id, Ts);
