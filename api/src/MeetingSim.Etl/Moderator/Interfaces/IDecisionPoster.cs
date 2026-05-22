using System.Text.Json.Nodes;

namespace MeetingSim.Etl.Moderator.Interfaces;

public interface IDecisionPoster
{
    Task PostDecision(JsonObject decision, CancellationToken cancellationToken = default);
}
