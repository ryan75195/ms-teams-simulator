using System.Text.Json;
using MeetingSim.Core.Personas;
using MeetingSim.Etl.Moderator.Interfaces;
using MeetingSim.Etl.Moderator.Orchestrator.Interfaces;
using OpenAI.Chat;

namespace MeetingSim.Etl.Moderator.Orchestrator.Tools;

internal sealed class SetActiveResponderTool : IModeratorTool
{
    public const string ToolName = "set_active_responder";

    private readonly IModeratorStateMutator _state;
    private readonly ChatTool _definition;

    public SetActiveResponderTool(IReadOnlyList<Persona> roster, IModeratorStateMutator state)
    {
        _state = state;
        _definition = ChatTool.CreateFunctionTool(
            functionName: ToolName,
            functionDescription:
                "Set or clear who the active responder is for upcoming presenter follow-ups. Pass "
                + "persona_id to lock dialogue continuity to that persona. Omit persona_id to clear "
                + "the slot when the presenter has wrapped up a dialogue thread or pivoted to a new "
                + "topic. cast_speak already sets the speaker as active responder by default — only "
                + "call this tool to override that default.",
            functionParameters: BuildSchema(roster));
    }

    public string Name => ToolName;

    public ChatTool Definition => _definition;

    public Task Execute(JsonElement arguments, ModeratorContext context, CancellationToken cancellationToken)
    {
        string? newId = null;
        if (arguments.TryGetProperty("persona_id", out var el) && el.GetString() is { Length: > 0 } id)
        {
            newId = id;
        }
        _state.SetActiveResponder(newId);
        return Task.CompletedTask;
    }

    private static BinaryData BuildSchema(IReadOnlyList<Persona> roster)
    {
        var enumLiteral = BuildPersonaEnum(roster);
        return BinaryData.FromString($$"""
            {
              "type": "object",
              "properties": {
                "persona_id": { "type": "string", "enum": {{enumLiteral}} }
              },
              "additionalProperties": false
            }
            """);
    }

    private static string BuildPersonaEnum(IReadOnlyList<Persona> roster)
    {
        var ids = roster
            .Where(p => p.Archetype != Archetype.User)
            .Select(p => "\"" + p.Id + "\"");
        return "[" + string.Join(",", ids) + "]";
    }
}
