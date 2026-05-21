namespace MeetingSim.Api.Transcription.Interfaces;

public interface ITranscriptionService
{
    Task<string> Transcribe(
        Stream audio,
        string fileName,
        CancellationToken cancellationToken = default);
}
