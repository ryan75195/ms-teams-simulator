namespace MeetingSim.Core.Events;

public sealed record ReactionEvent(
    long Id,
    DateTimeOffset Ts,
    int Tile,
    string Emoji) : MeetingEvent(Id, Ts);
