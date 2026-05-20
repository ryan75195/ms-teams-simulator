namespace MeetingSim.Core.Sessions.Interfaces;

public interface ISessionStore
{
    Session Create(string title, int audienceSize, SessionSettings settings);
    Session? TryGet(Guid id);
    IReadOnlyList<Session> List();
    bool Remove(Guid id);
    Session? Update(Guid id, SessionSettings settings);
}
