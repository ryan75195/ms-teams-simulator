using System.Net.WebSockets;
using System.Text.Json;
using System.Text.Json.Nodes;
using MeetingSim.Api.Realtime.Interfaces;

namespace MeetingSim.Api.Realtime;

internal sealed class OpenAIRealtimeTranscriptionClient : IRealtimeTranscriptionClient
{
    public const string TranscriptionModelName = "gpt-4o-mini-transcribe";

    private const string RealtimeEndpoint = "wss://api.openai.com/v1/realtime?intent=transcription";

    private readonly string _apiKey;

    public OpenAIRealtimeTranscriptionClient(IConfiguration configuration)
    {
        _apiKey = configuration["OpenAI:ApiKey"]
            ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY")
            ?? throw new InvalidOperationException(
                "OPENAI_API_KEY is not configured. Set the env var or OpenAI:ApiKey configuration.");
    }

    public const int AudioSampleRateHz = 24_000;

    public async Task<IRealtimeTranscriptionSession> Open(CancellationToken cancellationToken)
    {
        var socket = new ClientWebSocket();
        socket.Options.SetRequestHeader("Authorization", $"Bearer {_apiKey}");
        var disposeOnFailure = socket;
        try
        {
            await socket.ConnectAsync(new Uri(RealtimeEndpoint), cancellationToken).ConfigureAwait(false);
            await SendInitialUpdate(socket, cancellationToken).ConfigureAwait(false);
            var session = new OpenAIRealtimeTranscriptionSession(socket);
            disposeOnFailure = null;
            return session;
        }
        finally
        {
            disposeOnFailure?.Dispose();
        }
    }

    private static async Task SendInitialUpdate(ClientWebSocket socket, CancellationToken cancellationToken)
    {
        var payload = new JsonObject
        {
            ["type"] = "session.update",
            ["session"] = new JsonObject
            {
                ["type"] = "transcription",
                ["audio"] = new JsonObject
                {
                    ["input"] = new JsonObject
                    {
                        ["format"] = new JsonObject
                        {
                            ["type"] = "audio/pcm",
                            ["rate"] = AudioSampleRateHz,
                        },
                        ["transcription"] = new JsonObject
                        {
                            ["model"] = TranscriptionModelName,
                        },
                        ["turn_detection"] = new JsonObject
                        {
                            ["type"] = "server_vad",
                            ["threshold"] = 0.5,
                            ["prefix_padding_ms"] = 300,
                            ["silence_duration_ms"] = 800,
                        },
                    },
                },
            },
        };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload);
        await socket
            .SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, cancellationToken)
            .ConfigureAwait(false);
    }
}
