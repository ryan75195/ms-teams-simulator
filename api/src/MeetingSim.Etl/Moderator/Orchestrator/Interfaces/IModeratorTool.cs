using System.Text.Json;
using OpenAI.Chat;

namespace MeetingSim.Etl.Moderator.Orchestrator.Interfaces;

public interface IModeratorTool
{
    string Name { get; }

    ChatTool Definition { get; }

    Task Execute(JsonElement arguments, ModeratorContext context, CancellationToken cancellationToken);
}
