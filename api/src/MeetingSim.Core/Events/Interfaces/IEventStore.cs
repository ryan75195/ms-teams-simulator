namespace MeetingSim.Core.Events.Interfaces;

public interface IEventStore
{
    MeetingEvent Append(Guid sessionId, Func<long, DateTimeOffset, MeetingEvent> factory);

    IReadOnlyList<MeetingEvent> ReadSince(Guid sessionId, long sinceId);

    void Clear(Guid sessionId);
}
