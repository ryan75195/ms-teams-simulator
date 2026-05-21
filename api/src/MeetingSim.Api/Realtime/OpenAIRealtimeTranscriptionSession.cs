using System.Buffers;
using System.Net.WebSockets;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using MeetingSim.Api.Realtime.Interfaces;

namespace MeetingSim.Api.Realtime;

internal sealed class OpenAIRealtimeTranscriptionSession : IRealtimeTranscriptionSession
{
    private const int ReceiveBufferSize = 32 * 1024;
    private const string DeltaEventType = "conversation.item.input_audio_transcription.delta";
    private const string CompletedEventType = "conversation.item.input_audio_transcription.completed";

    private readonly ClientWebSocket _socket;
    private readonly Channel<TranscriptionEvent> _events;
    private readonly CancellationTokenSource _pumpCts;
    private readonly Task _pumpTask;
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    public OpenAIRealtimeTranscriptionSession(ClientWebSocket connectedSocket)
    {
        _socket = connectedSocket;
        _events = Channel.CreateUnbounded<TranscriptionEvent>(new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = true,
        });
        _pumpCts = new CancellationTokenSource();
        _pumpTask = Task.Run(() => ReadLoop(_pumpCts.Token));
    }

    public ChannelReader<TranscriptionEvent> Events => _events.Reader;

    public async ValueTask SendAudio(ReadOnlyMemory<byte> pcm16, CancellationToken cancellationToken)
    {
        var payload = new JsonObject
        {
            ["type"] = "input_audio_buffer.append",
            ["audio"] = Convert.ToBase64String(pcm16.Span),
        };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload);
        await _sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _socket
                .SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _pumpCts.CancelAsync().ConfigureAwait(false);
        await CloseSocketQuietly().ConfigureAwait(false);
        try
        {
            await _pumpTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        _events.Writer.TryComplete();
        _socket.Dispose();
        _pumpCts.Dispose();
        _sendLock.Dispose();
    }

    private async Task CloseSocketQuietly()
    {
        if (_socket.State != WebSocketState.Open)
        {
            return;
        }
        try
        {
            await _socket
                .CloseAsync(WebSocketCloseStatus.NormalClosure, "client_dispose", CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (WebSocketException)
        {
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task ReadLoop(CancellationToken cancellationToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(ReceiveBufferSize);
        try
        {
            while (!cancellationToken.IsCancellationRequested && _socket.State == WebSocketState.Open)
            {
                var message = await ReadOneMessage(buffer, cancellationToken).ConfigureAwait(false);
                if (message is null)
                {
                    break;
                }
                ParseAndDispatch(message);
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
            _events.Writer.TryComplete();
        }
    }

    private async Task<byte[]?> ReadOneMessage(byte[] buffer, CancellationToken cancellationToken)
    {
        await using var ms = new MemoryStream();
        while (true)
        {
            var result = await _socket
                .ReceiveAsync(buffer, cancellationToken)
                .ConfigureAwait(false);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                return null;
            }
            await ms.WriteAsync(buffer.AsMemory(0, result.Count), cancellationToken).ConfigureAwait(false);
            if (result.EndOfMessage)
            {
                return ms.ToArray();
            }
        }
    }

    private void ParseAndDispatch(byte[] json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("type", out var typeEl))
            {
                return;
            }
            DispatchByType(typeEl.GetString(), root);
        }
        catch (JsonException)
        {
        }
    }

    private void DispatchByType(string? type, JsonElement root)
    {
        if (type == DeltaEventType)
        {
            var text = root.TryGetProperty("delta", out var d) ? d.GetString() ?? string.Empty : string.Empty;
            if (text.Length > 0)
            {
                _events.Writer.TryWrite(new TranscriptionEvent(IsFinal: false, Text: text));
            }
        }
        else if (type == CompletedEventType)
        {
            var text = root.TryGetProperty("transcript", out var t) ? t.GetString() ?? string.Empty : string.Empty;
            if (text.Length > 0)
            {
                _events.Writer.TryWrite(new TranscriptionEvent(IsFinal: true, Text: text));
            }
        }
    }
}
