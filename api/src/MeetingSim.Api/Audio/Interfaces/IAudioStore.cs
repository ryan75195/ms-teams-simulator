namespace MeetingSim.Api.Audio.Interfaces;

public interface IAudioStore
{
    void Put(Guid sessionId, long eventId, AudioClip clip);

    AudioClip? TryGet(Guid sessionId, long eventId);

    bool Remove(Guid sessionId, long eventId);
}
