namespace MeetingSim.Core.Events;

public sealed record HandRaiseEvent(
    long Id,
    DateTimeOffset Ts,
    string PersonaId,
    bool Raised) : MeetingEvent(Id, Ts);
