using System.Text.Json;
using System.Text.Json.Nodes;
using MeetingSim.Core.Personas;
using MeetingSim.Etl.Moderator.Interfaces;
using MeetingSim.Etl.Moderator.Orchestrator.Interfaces;
using OpenAI.Chat;

namespace MeetingSim.Etl.Moderator.Orchestrator.Tools;

internal sealed class ReactTool : IModeratorTool
{
    public const string ToolName = "react";

    private static readonly string[] AllowedEmoji = ["👍", "👏", "❤️", "🙂", "🎉", "😮"];

    private readonly IEventPoster _poster;
    private readonly ChatTool _definition;
    private readonly Dictionary<string, int> _tileByPersonaId;

    public ReactTool(IReadOnlyList<Persona> roster, IEventPoster poster)
    {
        _poster = poster;
        _tileByPersonaId = BuildTileMap(roster);
        _definition = ChatTool.CreateFunctionTool(
            functionName: ToolName,
            functionDescription:
                "A persona reacts with an emoji floating up from their tile. The least intrusive action — "
                + "use for wins, surprises, applause moments, or low-effort engagement.",
            functionParameters: BuildSchema(roster));
    }

    public string Name => ToolName;

    public ChatTool Definition => _definition;

    public async Task Execute(JsonElement arguments, ModeratorContext context, CancellationToken cancellationToken)
    {
        if (!arguments.TryGetProperty("persona_id", out var idEl) || idEl.GetString() is not { Length: > 0 } personaId)
        {
            await Console.Error.WriteLineAsync($"[{ToolName}] missing persona_id").ConfigureAwait(false);
            return;
        }
        if (!arguments.TryGetProperty("emoji", out var emojiEl) || emojiEl.GetString() is not { Length: > 0 } emoji)
        {
            await Console.Error.WriteLineAsync($"[{ToolName}] missing emoji").ConfigureAwait(false);
            return;
        }
        if (!_tileByPersonaId.TryGetValue(personaId, out var tile))
        {
            await Console.Error.WriteLineAsync($"[{ToolName}] unknown persona_id '{personaId}'").ConfigureAwait(false);
            return;
        }
        var body = new JsonObject
        {
            ["kind"] = "reaction",
            ["tile"] = tile,
            ["emoji"] = emoji,
        };
        await _poster.PostEvent(body, cancellationToken).ConfigureAwait(false);
    }

    private static Dictionary<string, int> BuildTileMap(IReadOnlyList<Persona> roster)
    {
        var map = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < roster.Count; i++)
        {
            map[roster[i].Id] = i;
        }
        return map;
    }

    private static BinaryData BuildSchema(IReadOnlyList<Persona> roster)
    {
        var personaEnum = "[" + string.Join(",", roster
            .Where(p => p.Archetype != Archetype.User)
            .Select(p => "\"" + p.Id + "\"")) + "]";
        var emojiEnum = "[" + string.Join(",", AllowedEmoji.Select(e => "\"" + e + "\"")) + "]";
        return BinaryData.FromString($$"""
            {
              "type": "object",
              "properties": {
                "persona_id": { "type": "string", "enum": {{personaEnum}} },
                "emoji":      { "type": "string", "enum": {{emojiEnum}} }
              },
              "required": ["persona_id", "emoji"],
              "additionalProperties": false
            }
            """);
    }
}
