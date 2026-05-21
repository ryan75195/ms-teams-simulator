namespace MeetingSim.Core.Events;

public sealed record TranscriptChunkEvent(
    long Id,
    DateTimeOffset Ts,
    string Text,
    bool IsFinal) : MeetingEvent(Id, Ts);
