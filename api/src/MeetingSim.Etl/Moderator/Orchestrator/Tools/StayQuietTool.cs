using System.Text.Json;
using MeetingSim.Etl.Moderator.Orchestrator.Interfaces;
using OpenAI.Chat;

namespace MeetingSim.Etl.Moderator.Orchestrator.Tools;

internal sealed class StayQuietTool : IModeratorTool
{
    public const string ToolName = "stay_quiet";

    private static readonly BinaryData ParametersSchema = BinaryData.FromString("""
        {
          "type": "object",
          "properties": {
            "reason": { "type": "string", "description": "Why no audience action is warranted." }
          },
          "required": [],
          "additionalProperties": false
        }
        """);

    public string Name => ToolName;

    public ChatTool Definition => ChatTool.CreateFunctionTool(
        functionName: ToolName,
        functionDescription:
            "No one in the audience should react. Use for intros, transitions, throat-clearing, " +
            "obvious STT garbles, or anything that wouldn't naturally provoke a response in a real meeting.",
        functionParameters: ParametersSchema);

    public Task Execute(JsonElement arguments, ModeratorContext context, CancellationToken cancellationToken)
        => Task.CompletedTask;
}
