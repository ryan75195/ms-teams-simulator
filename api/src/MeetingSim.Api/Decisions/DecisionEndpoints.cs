using System.Text.Json.Nodes;
using MeetingSim.Api.Sessions.Interfaces;
using MeetingSim.Core.Sessions.Interfaces;

namespace MeetingSim.Api.Decisions;

public static class DecisionEndpoints
{
    public static IEndpointRouteBuilder MapDecisionEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/sessions/{id:guid}/decisions", AppendDecision);
        return app;
    }

    private static async Task<IResult> AppendDecision(
        Guid id,
        JsonObject body,
        ISessionStore sessions,
        ISessionArchive archive,
        CancellationToken cancellationToken)
    {
        if (sessions.TryGet(id) is null)
        {
            return Results.NotFound();
        }
        await archive.AppendDecision(id, body, cancellationToken).ConfigureAwait(false);
        return Results.NoContent();
    }
}
