using System.Collections.Concurrent;
using MeetingSim.Core.Events.Interfaces;

namespace MeetingSim.Core.Events;

public sealed class EventStore : IEventStore
{
    public const int MaxEventsPerSession = 1024;

    private sealed class SessionBuffer
    {
        private readonly List<MeetingEvent> _events = new(MaxEventsPerSession + 8);
        private long _nextId;
        private readonly Lock _lock = new();

        public MeetingEvent Append(Func<long, DateTimeOffset, MeetingEvent> factory)
        {
            lock (_lock)
            {
                _nextId++;
                var evt = factory(_nextId, DateTimeOffset.UtcNow);
                _events.Add(evt);
                if (_events.Count > MaxEventsPerSession)
                {
                    _events.RemoveRange(0, _events.Count - MaxEventsPerSession);
                }
                return evt;
            }
        }

        public IReadOnlyList<MeetingEvent> ReadSince(long sinceId)
        {
            lock (_lock)
            {
                var hits = new List<MeetingEvent>();
                foreach (var evt in _events)
                {
                    if (evt.Id > sinceId)
                    {
                        hits.Add(evt);
                    }
                }
                return hits;
            }
        }
    }

    private readonly ConcurrentDictionary<Guid, SessionBuffer> _buffers = new();

    public MeetingEvent Append(Guid sessionId, Func<long, DateTimeOffset, MeetingEvent> factory)
    {
        var buffer = _buffers.GetOrAdd(sessionId, _ => new SessionBuffer());
        return buffer.Append(factory);
    }

    public IReadOnlyList<MeetingEvent> ReadSince(Guid sessionId, long sinceId)
    {
        if (_buffers.TryGetValue(sessionId, out var buffer))
        {
            return buffer.ReadSince(sinceId);
        }
        return Array.Empty<MeetingEvent>();
    }

    public void Clear(Guid sessionId) => _buffers.TryRemove(sessionId, out _);
}
