using System.Text.Json.Nodes;

namespace MeetingSim.Etl.Moderator.Interfaces;

public interface IEventPoster
{
    Task PostEvent(JsonObject body, CancellationToken cancellationToken = default);
}
