namespace MeetingSim.Api.Realtime.Interfaces;

public interface IRealtimeTranscriptionClient
{
    Task<IRealtimeTranscriptionSession> Open(CancellationToken cancellationToken);
}
