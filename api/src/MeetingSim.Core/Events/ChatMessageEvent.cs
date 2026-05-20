namespace MeetingSim.Core.Events;

public sealed record ChatMessageEvent(
    long Id,
    DateTimeOffset Ts,
    string PersonaId,
    string Text) : MeetingEvent(Id, Ts);
