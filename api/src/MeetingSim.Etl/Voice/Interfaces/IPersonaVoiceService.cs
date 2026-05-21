namespace MeetingSim.Etl.Voice.Interfaces;

public interface IPersonaVoiceService
{
    Task<string> GenerateLine(
        string personaId,
        string presenterLine,
        IReadOnlyList<string> recentChunks,
        IReadOnlyList<string> personaPreviousLines,
        CancellationToken cancellationToken = default);
}
