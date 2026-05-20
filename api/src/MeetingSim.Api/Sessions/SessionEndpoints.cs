using MeetingSim.Api.Contracts;
using MeetingSim.Core.Sessions;
using MeetingSim.Core.Sessions.Interfaces;

namespace MeetingSim.Api.Sessions;

public static class SessionEndpoints
{
    private static readonly SessionSettings DefaultSettings = new(
        Engagement: 5,
        Noise: 2,
        AutoChat: true,
        AutoReactions: true);

    public static IEndpointRouteBuilder MapSessionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/sessions");
        group.MapPost("/", CreateSession);
        group.MapGet("/", ListSessions);
        group.MapGet("/{id:guid}", GetSession);
        group.MapPatch("/{id:guid}", UpdateSettings);
        group.MapDelete("/{id:guid}", DeleteSession);
        return app;
    }

    private static IResult CreateSession(CreateSessionRequest request, ISessionStore store)
    {
        var settings = request.Settings ?? DefaultSettings;
        var session = store.Create(request.Title, request.AudienceSize, settings);
        return Results.Created($"/sessions/{session.Id}", session);
    }

    private static IResult ListSessions(ISessionStore store)
        => Results.Ok(store.List());

    private static IResult GetSession(Guid id, ISessionStore store)
        => store.TryGet(id) is { } session
            ? Results.Ok(session)
            : Results.NotFound();

    private static IResult UpdateSettings(Guid id, SessionSettings settings, ISessionStore store)
        => store.Update(id, settings) is { } session
            ? Results.Ok(session)
            : Results.NotFound();

    private static IResult DeleteSession(Guid id, ISessionStore store)
        => store.Remove(id)
            ? Results.NoContent()
            : Results.NotFound();
}
