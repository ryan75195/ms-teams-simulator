using System.Collections.Concurrent;
using MeetingSim.Api.Audio.Interfaces;

namespace MeetingSim.Api.Audio;

public sealed class AudioStore : IAudioStore
{
    private readonly ConcurrentDictionary<(Guid SessionId, long EventId), AudioClip> _clips = new();

    public void Put(Guid sessionId, long eventId, AudioClip clip)
    {
        _clips[(sessionId, eventId)] = clip;
    }

    public AudioClip? TryGet(Guid sessionId, long eventId)
    {
        return _clips.TryGetValue((sessionId, eventId), out var clip) ? clip : null;
    }

    public bool Remove(Guid sessionId, long eventId)
    {
        return _clips.TryRemove((sessionId, eventId), out _);
    }
}
