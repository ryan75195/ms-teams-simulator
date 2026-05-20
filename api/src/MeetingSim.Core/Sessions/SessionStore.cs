using System.Collections.Concurrent;
using System.Security.Cryptography;
using MeetingSim.Core.Sessions.Interfaces;

namespace MeetingSim.Core.Sessions;

public sealed class SessionStore : ISessionStore
{
    private readonly ConcurrentDictionary<Guid, Session> _sessions = new();

    public Session Create(string title, int audienceSize, SessionSettings settings)
    {
        var session = new Session(
            Guid.NewGuid(),
            title,
            audienceSize,
            RandomNumberGenerator.GetInt32(int.MaxValue),
            DateTimeOffset.UtcNow,
            settings);

        _sessions[session.Id] = session;
        return session;
    }

    public Session? TryGet(Guid id)
        => _sessions.TryGetValue(id, out var session) ? session : null;

    public IReadOnlyList<Session> List() => _sessions.Values.ToList();

    public bool Remove(Guid id) => _sessions.TryRemove(id, out _);

    public Session? Update(Guid id, SessionSettings settings)
    {
        if (!_sessions.TryGetValue(id, out var existing))
        {
            return null;
        }

        var updated = existing with { Settings = settings };
        _sessions[id] = updated;
        return updated;
    }
}
