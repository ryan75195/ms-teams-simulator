using MeetingSim.Api.Audio.Interfaces;
using MeetingSim.Core.Sessions.Interfaces;

namespace MeetingSim.Api.Audio;

public static class AudioEndpoints
{
    public static IEndpointRouteBuilder MapAudioEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/sessions/{id:guid}/audio/{eventId:long}", GetAudio);
        return app;
    }

    private static IResult GetAudio(
        Guid id,
        long eventId,
        ISessionStore sessions,
        IAudioStore audio)
    {
        if (sessions.TryGet(id) is null)
        {
            return Results.NotFound();
        }

        var clip = audio.TryGet(id, eventId);
        if (clip is null)
        {
            return Results.NotFound();
        }

        return Results.Bytes(clip.Bytes.ToArray(), clip.ContentType);
    }
}
