using System.Text.Json.Nodes;
using MeetingSim.Core.Events;

namespace MeetingSim.Api.Sessions.Interfaces;

public interface ISessionArchive
{
    Task SaveManifest(SessionManifest manifest, CancellationToken cancellationToken = default);

    Task<SessionManifest?> LoadManifest(Guid sessionId, CancellationToken cancellationToken = default);

    Task AppendEvent(Guid sessionId, MeetingEvent evt, CancellationToken cancellationToken = default);

    Task AppendDecision(Guid sessionId, JsonObject decision, CancellationToken cancellationToken = default);

    Task WriteAudio(Guid sessionId, long eventId, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken = default);

    Task<Stream?> OpenAudio(Guid sessionId, long eventId, CancellationToken cancellationToken = default);

    IAsyncEnumerable<string> ReadEventLines(Guid sessionId, CancellationToken cancellationToken = default);

    string GetSessionDirectory(Guid sessionId);

    IReadOnlyList<Guid> ListArchivedSessions();
}
