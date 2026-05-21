using System.ClientModel;
using MeetingSim.Api.Transcription.Interfaces;
using OpenAI.Audio;

namespace MeetingSim.Api.Transcription;

internal sealed class OpenAITranscriptionService : ITranscriptionService
{
    public const string ModelName = "gpt-4o-mini-transcribe";

    private readonly AudioClient _client;

    public OpenAITranscriptionService(IConfiguration configuration)
    {
        var apiKey = configuration["OpenAI:ApiKey"]
            ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY")
            ?? throw new InvalidOperationException(
                "OPENAI_API_KEY is not configured. Set the env var or OpenAI:ApiKey configuration.");

        _client = new AudioClient(model: ModelName, apiKey: apiKey);
    }

    public async Task<string> Transcribe(
        Stream audio,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        ClientResult<AudioTranscription> result = await _client
            .TranscribeAudioAsync(audio, fileName, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return result.Value.Text;
    }
}
