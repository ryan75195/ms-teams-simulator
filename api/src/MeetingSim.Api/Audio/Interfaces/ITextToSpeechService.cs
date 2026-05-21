namespace MeetingSim.Api.Audio.Interfaces;

public interface ITextToSpeechService
{
    Task<AudioClip> Generate(
        string personaId,
        string text,
        CancellationToken cancellationToken = default);
}
