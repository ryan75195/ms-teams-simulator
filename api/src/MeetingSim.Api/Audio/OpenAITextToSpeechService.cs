using System.ClientModel;
using MeetingSim.Api.Audio.Interfaces;
using OpenAI.Audio;

namespace MeetingSim.Api.Audio;

internal sealed class OpenAITextToSpeechService : ITextToSpeechService
{
    public const string ModelName = "gpt-4o-mini-tts";
    public const string AudioContentType = "audio/mpeg";

    private static readonly Dictionary<string, GeneratedSpeechVoice> PersonaVoices = new(StringComparer.Ordinal)
    {
        ["anuj"] = GeneratedSpeechVoice.Onyx,
        ["serena"] = GeneratedSpeechVoice.Nova,
        ["kayo"] = GeneratedSpeechVoice.Shimmer,
        ["isaac"] = GeneratedSpeechVoice.Echo,
        ["charlotte"] = GeneratedSpeechVoice.Fable,
        ["danielle"] = GeneratedSpeechVoice.Alloy,
        ["ray"] = GeneratedSpeechVoice.Onyx,
        ["bryan"] = GeneratedSpeechVoice.Echo,
        ["eva"] = GeneratedSpeechVoice.Nova,
        ["krystal"] = GeneratedSpeechVoice.Shimmer,
        ["alvin"] = GeneratedSpeechVoice.Fable,
    };

    private static readonly GeneratedSpeechVoice FallbackVoice = GeneratedSpeechVoice.Alloy;

    private readonly AudioClient _client;

    public OpenAITextToSpeechService(IConfiguration configuration)
    {
        var apiKey = configuration["OpenAI:ApiKey"]
            ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY")
            ?? throw new InvalidOperationException(
                "OPENAI_API_KEY is not configured. Set the env var or OpenAI:ApiKey configuration.");

        _client = new AudioClient(model: ModelName, apiKey: apiKey);
    }

    public async Task<AudioClip> Generate(
        string personaId,
        string text,
        CancellationToken cancellationToken = default)
    {
        var voice = PersonaVoices.TryGetValue(personaId, out var mapped) ? mapped : FallbackVoice;

        var options = new SpeechGenerationOptions
        {
            ResponseFormat = GeneratedSpeechFormat.Mp3,
        };

        ClientResult<BinaryData> result = await _client
            .GenerateSpeechAsync(text, voice, options, cancellationToken)
            .ConfigureAwait(false);

        return new AudioClip(result.Value.ToArray(), AudioContentType);
    }
}
