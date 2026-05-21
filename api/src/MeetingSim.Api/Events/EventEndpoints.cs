using MeetingSim.Api.Audio.Interfaces;
using MeetingSim.Api.Contracts;
using MeetingSim.Core.Events;
using MeetingSim.Core.Events.Interfaces;
using MeetingSim.Core.Sessions.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace MeetingSim.Api.Events;

public static class EventEndpoints
{
    public static IEndpointRouteBuilder MapEventEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/sessions/{id:guid}/events");
        group.MapPost("/", AppendEvent);
        group.MapGet("/", ReadSinceEvents);
        return app;
    }

    private static async Task<IResult> AppendEvent(
        Guid id,
        EventRequest request,
        ISessionStore sessions,
        IEventStore events,
        IHubContext<SessionHub> hub,
        ITextToSpeechService tts,
        IAudioStore audio,
        CancellationToken cancellationToken)
    {
        if (sessions.TryGet(id) is null)
        {
            return Results.NotFound();
        }

        var appended = events.Append(id, (eventId, ts) => Materialise(request, eventId, ts));

        if (appended is SpeakEvent speak && !string.IsNullOrWhiteSpace(speak.Text))
        {
            await SynthesiseAndStore(id, speak, tts, audio, cancellationToken).ConfigureAwait(false);
        }

        await hub.Clients
            .Group(SessionHub.GroupName(id))
            .SendAsync(SessionHub.EventMethodName, appended, cancellationToken)
            .ConfigureAwait(false);

        return Results.Created($"/sessions/{id}/events/{appended.Id}", appended);
    }

    private static async Task SynthesiseAndStore(
        Guid sessionId,
        SpeakEvent speak,
        ITextToSpeechService tts,
        IAudioStore audio,
        CancellationToken cancellationToken)
    {
        var clip = await tts
            .Generate(speak.PersonaId, speak.Text, cancellationToken)
            .ConfigureAwait(false);

        audio.Put(sessionId, speak.Id, clip);
    }

    private static IResult ReadSinceEvents(
        Guid id,
        long? since,
        ISessionStore sessions,
        IEventStore events)
    {
        if (sessions.TryGet(id) is null)
        {
            return Results.NotFound();
        }

        return Results.Ok(events.ReadSince(id, since ?? 0));
    }

    private static MeetingEvent Materialise(EventRequest request, long id, DateTimeOffset ts) => request switch
    {
        SpeakEventRequest s => new SpeakEvent(id, ts, s.PersonaId, s.Text, s.DurationMs),
        HandRaiseEventRequest h => new HandRaiseEvent(id, ts, h.PersonaId, h.Raised),
        ChatMessageEventRequest c => new ChatMessageEvent(id, ts, c.PersonaId, c.Text),
        ReactionEventRequest r => new ReactionEvent(id, ts, r.Tile, r.Emoji),
        SlideUpdateEventRequest u => new SlideUpdateEvent(id, ts, u.Text),
        _ => throw new InvalidOperationException($"Unknown event request type {request.GetType().Name}"),
    };
}
