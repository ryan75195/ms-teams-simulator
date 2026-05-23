using System.Text.Json;
using System.Text.Json.Nodes;
using MeetingSim.Core.Personas;
using MeetingSim.Etl.Moderator.Interfaces;
using MeetingSim.Etl.Moderator.Orchestrator.Interfaces;
using OpenAI.Chat;

namespace MeetingSim.Etl.Moderator.Orchestrator.Tools;

internal sealed class LowerHandTool : IModeratorTool
{
    public const string ToolName = "lower_hand";

    private readonly IEventPoster _poster;
    private readonly ChatTool _definition;

    public LowerHandTool(IReadOnlyList<Persona> roster, IEventPoster poster)
    {
        _poster = poster;
        _definition = ChatTool.CreateFunctionTool(
            functionName: ToolName,
            functionDescription:
                "Lower a persona's hand. Use when the topic has shifted and a previously raised hand "
                + "is no longer relevant — keeps the meeting from accumulating stale hand-raises.",
            functionParameters: BuildSchema(roster));
    }

    public string Name => ToolName;

    public ChatTool Definition => _definition;

    public async Task Execute(JsonElement arguments, ModeratorContext context, CancellationToken cancellationToken)
    {
        if (!TryReadPersonaId(arguments, out var personaId))
        {
            await Console.Error.WriteLineAsync($"[{ToolName}] missing persona_id argument").ConfigureAwait(false);
            return;
        }
        var body = new JsonObject
        {
            ["kind"] = "hand-raise",
            ["personaId"] = personaId,
            ["raised"] = false,
        };
        await _poster.PostEvent(body, cancellationToken).ConfigureAwait(false);
    }

    private static bool TryReadPersonaId(JsonElement args, out string personaId)
    {
        personaId = string.Empty;
        if (!args.TryGetProperty("persona_id", out var el))
        {
            return false;
        }
        var value = el.GetString();
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }
        personaId = value;
        return true;
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
              "required": ["persona_id"],
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
