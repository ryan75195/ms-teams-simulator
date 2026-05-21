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
        IHubContext<SessionHub> hub)
    {
        if (sessions.TryGet(id) is null)
        {
            return Results.NotFound();
        }

        var appended = events.Append(id, (eventId, ts) => Materialise(request, eventId, ts));
        await hub.Clients
            .Group(SessionHub.GroupName(id))
            .SendAsync(SessionHub.EventMethodName, appended)
            .ConfigureAwait(false);

        return Results.Created($"/sessions/{id}/events/{appended.Id}", appended);
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
        SpeakEventRequest s => new SpeakEvent(id, ts, s.PersonaId, s.DurationMs),
        HandRaiseEventRequest h => new HandRaiseEvent(id, ts, h.PersonaId, h.Raised),
        ChatMessageEventRequest c => new ChatMessageEvent(id, ts, c.PersonaId, c.Text),
        ReactionEventRequest r => new ReactionEvent(id, ts, r.Tile, r.Emoji),
        _ => throw new InvalidOperationException($"Unknown event request type {request.GetType().Name}"),
    };
}
