using MeetingSim.Api.Events;
using MeetingSim.Api.Transcription.Interfaces;
using MeetingSim.Core.Events;
using MeetingSim.Core.Events.Interfaces;
using MeetingSim.Core.Sessions.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace MeetingSim.Api.Transcription;

public static class TranscribeEndpoints
{
    public static IEndpointRouteBuilder MapTranscribeEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/sessions/{id:guid}/transcribe", TranscribeAudio)
            .DisableAntiforgery();
        return app;
    }

    private static async Task<IResult> TranscribeAudio(
        Guid id,
        [FromForm] IFormFile file,
        ISessionStore sessions,
        IEventStore events,
        ITranscriptionService transcription,
        IHubContext<SessionHub> hub,
        CancellationToken cancellationToken)
    {
        if (sessions.TryGet(id) is null)
        {
            return Results.NotFound();
        }

        if (file is null || file.Length == 0)
        {
            return Results.BadRequest("Missing or empty file part.");
        }

        await using var stream = file.OpenReadStream();
        var text = await transcription
            .Transcribe(stream, file.FileName, cancellationToken)
            .ConfigureAwait(false);

        var appended = events.Append(id, (eventId, ts) =>
            new TranscriptChunkEvent(eventId, ts, text, IsFinal: true));

        await hub.Clients
            .Group(SessionHub.GroupName(id))
            .SendAsync(SessionHub.EventMethodName, appended, cancellationToken)
            .ConfigureAwait(false);

        return Results.Created($"/sessions/{id}/events/{appended.Id}", appended);
    }
}
