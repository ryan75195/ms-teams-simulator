using System.Threading.Channels;

namespace MeetingSim.Api.Realtime.Interfaces;

public interface IRealtimeTranscriptionSession : IAsyncDisposable
{
    ChannelReader<TranscriptionEvent> Events { get; }

    ValueTask SendAudio(ReadOnlyMemory<byte> pcm16, CancellationToken cancellationToken);
}
