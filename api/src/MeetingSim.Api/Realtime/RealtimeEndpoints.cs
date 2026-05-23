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
    private static readonly TimeSpan MaxSessionDuration = TimeSpan.FromHours(4);

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
        using var sessionCts = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
        sessionCts.CancelAfter(MaxSessionDuration);
        await using var transcription = await client.Open(sessionCts.Token).ConfigureAwait(false);
        await BridgePumps(id, browser, transcription, events, hub, sessionCts.Token).ConfigureAwait(false);
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
        var frameCount = 0;
        var byteCount = 0L;
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
                    frameCount++;
                    byteCount += frame.Length;
                    await LogFrameProgress(frameCount, byteCount).ConfigureAwait(false);
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
            await Console.Out
                .WriteLineAsync($"[realtime] -> pump ended, {frameCount} frames / {byteCount} bytes")
                .ConfigureAwait(false);
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static async Task LogFrameProgress(int frameCount, long byteCount)
    {
        if (frameCount != 1 && frameCount % 50 != 0)
        {
            return;
        }
        await Console.Out
            .WriteLineAsync($"[realtime] -> {frameCount} audio frames sent ({byteCount} bytes total)")
            .ConfigureAwait(false);
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

    private const int PartialMilestoneWordThreshold = 8;

    private static async Task PumpOpenAIToBrowser(
        Guid sessionId,
        IRealtimeTranscriptionSession session,
        WebSocket browser,
        IEventStore events,
        IHubContext<SessionHub> hub,
        CancellationToken cancellationToken)
    {
        var accumulator = new System.Text.StringBuilder();
        var milestoneFired = false;
        try
        {
            await foreach (var evt in session.Events.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                if (evt.IsFinal)
                {
                    await BroadcastFinal(sessionId, evt.Text, events, hub, cancellationToken)
                        .ConfigureAwait(false);
                    accumulator.Clear();
                    milestoneFired = false;
                }
                else
                {
                    accumulator.Append(evt.Text);
                    milestoneFired = await MaybeBroadcastMilestone(
                        sessionId, accumulator.ToString(), milestoneFired, events, hub, cancellationToken)
                        .ConfigureAwait(false);
                    if (browser.State == WebSocketState.Open)
                    {
                        await SendPartialToBrowser(browser, evt.Text, cancellationToken).ConfigureAwait(false);
                    }
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

    private static async Task<bool> MaybeBroadcastMilestone(
        Guid sessionId,
        string accumulated,
        bool alreadyFired,
        IEventStore events,
        IHubContext<SessionHub> hub,
        CancellationToken cancellationToken)
    {
        if (alreadyFired)
        {
            return true;
        }
        if (CountWords(accumulated) < PartialMilestoneWordThreshold)
        {
            return false;
        }
        var appended = events.Append(sessionId, (eventId, ts) =>
            new TranscriptMilestoneEvent(eventId, ts, accumulated));
        await hub.Clients
            .Group(SessionHub.GroupName(sessionId))
            .SendAsync(SessionHub.EventMethodName, appended, cancellationToken)
            .ConfigureAwait(false);
        return true;
    }

    private static int CountWords(string text)
    {
        var count = 0;
        var inWord = false;
        foreach (var ch in text)
        {
            if (char.IsWhiteSpace(ch))
            {
                inWord = false;
            }
            else if (!inWord)
            {
                inWord = true;
                count++;
            }
        }
        return count;
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
