using System.Buffers;
using System.Net.WebSockets;
using System.Text.Json;
using System.Text.Json.Nodes;
using MeetingSim.Api.Events;
using MeetingSim.Api.Realtime.Interfaces;
using MeetingSim.Core.Events;
using MeetingSim.Core.Events.Interfaces;
using MeetingSim.Core.Sessions.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace MeetingSim.Api.Realtime;

public static class RealtimeEndpoints
{
    private const int BrowserReceiveBufferSize = 16 * 1024;
    private const string PartialMessageType = "transcript.partial";

    public static IEndpointRouteBuilder MapRealtimeEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/sessions/{id:guid}/realtime", AcceptRealtime);
        return app;
    }

    private static async Task AcceptRealtime(
        Guid id,
        HttpContext context,
        ISessionStore sessions,
        IEventStore events,
        IHubContext<SessionHub> hub,
        IRealtimeTranscriptionClient client)
    {
        if (sessions.TryGet(id) is null)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        using var browser = await context.WebSockets.AcceptWebSocketAsync().ConfigureAwait(false);
        await using var transcription = await client.Open(context.RequestAborted).ConfigureAwait(false);
        await BridgePumps(id, browser, transcription, events, hub, context.RequestAborted).ConfigureAwait(false);
        await CloseBrowserQuietly(browser).ConfigureAwait(false);
    }

    private static async Task BridgePumps(
        Guid sessionId,
        WebSocket browser,
        IRealtimeTranscriptionSession transcription,
        IEventStore events,
        IHubContext<SessionHub> hub,
        CancellationToken outerCancellation)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(outerCancellation);
        var cancellation = linkedCts.Token;

        var inbound = PumpBrowserToOpenAI(browser, transcription, cancellation);
        var outbound = PumpOpenAIToBrowser(sessionId, transcription, browser, events, hub, cancellation);
        try
        {
            await Task.WhenAny(inbound, outbound).ConfigureAwait(false);
        }
        finally
        {
            await linkedCts.CancelAsync().ConfigureAwait(false);
            await Task.WhenAll(inbound, outbound).ConfigureAwait(false);
        }
    }

    private static async Task PumpBrowserToOpenAI(
        WebSocket browser,
        IRealtimeTranscriptionSession session,
        CancellationToken cancellationToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(BrowserReceiveBufferSize);
        try
        {
            await using var frame = new MemoryStream();
            while (!cancellationToken.IsCancellationRequested && browser.State == WebSocketState.Open)
            {
                frame.SetLength(0);
                var messageType = await ReadOneBrowserFrame(browser, buffer, frame, cancellationToken)
                    .ConfigureAwait(false);
                if (messageType == WebSocketMessageType.Close)
                {
                    return;
                }
                if (messageType == WebSocketMessageType.Binary && frame.Length > 0)
                {
                    await session.SendAudio(frame.ToArray(), cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch (WebSocketException)
        {
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static async Task<WebSocketMessageType> ReadOneBrowserFrame(
        WebSocket browser,
        byte[] buffer,
        MemoryStream destination,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var result = await browser
                .ReceiveAsync(buffer, cancellationToken)
                .ConfigureAwait(false);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                return WebSocketMessageType.Close;
            }
            await destination.WriteAsync(buffer.AsMemory(0, result.Count), cancellationToken).ConfigureAwait(false);
            if (result.EndOfMessage)
            {
                return result.MessageType;
            }
        }
    }

    private static async Task PumpOpenAIToBrowser(
        Guid sessionId,
        IRealtimeTranscriptionSession session,
        WebSocket browser,
        IEventStore events,
        IHubContext<SessionHub> hub,
        CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var evt in session.Events.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                if (evt.IsFinal)
                {
                    await BroadcastFinal(sessionId, evt.Text, events, hub, cancellationToken)
                        .ConfigureAwait(false);
                }
                else if (browser.State == WebSocketState.Open)
                {
                    await SendPartialToBrowser(browser, evt.Text, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch (WebSocketException)
        {
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static async Task BroadcastFinal(
        Guid sessionId,
        string text,
        IEventStore events,
        IHubContext<SessionHub> hub,
        CancellationToken cancellationToken)
    {
        var appended = events.Append(sessionId, (eventId, ts) =>
            new TranscriptChunkEvent(eventId, ts, text, IsFinal: true));
        await hub.Clients
            .Group(SessionHub.GroupName(sessionId))
            .SendAsync(SessionHub.EventMethodName, appended, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task SendPartialToBrowser(WebSocket browser, string text, CancellationToken cancellationToken)
    {
        var payload = new JsonObject
        {
            ["type"] = PartialMessageType,
            ["text"] = text,
        };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload);
        await browser
            .SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task CloseBrowserQuietly(WebSocket browser)
    {
        if (browser.State != WebSocketState.Open && browser.State != WebSocketState.CloseReceived)
        {
            return;
        }
        try
        {
            await browser
                .CloseAsync(WebSocketCloseStatus.NormalClosure, "session_end", CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (WebSocketException)
        {
        }
        catch (OperationCanceledException)
        {
        }
    }
}
