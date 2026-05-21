using System.Text.Json;
using System.Text.Json.Nodes;
using MeetingSim.Core.Personas;
using MeetingSim.Etl.Moderator.Interfaces;
using MeetingSim.Etl.Moderator.Orchestrator.Interfaces;
using MeetingSim.Etl.Voice.Interfaces;
using OpenAI.Chat;

namespace MeetingSim.Etl.Moderator.Orchestrator.Tools;

internal sealed class SendChatTool : IModeratorTool
{
    public const string ToolName = "send_chat";

    private readonly IEventPoster _poster;
    private readonly IPersonaVoiceService _voice;
    private readonly ChatTool _definition;

    public SendChatTool(
        IReadOnlyList<Persona> roster,
        IEventPoster poster,
        IPersonaVoiceService voice)
    {
        _poster = poster;
        _voice = voice;
        _definition = ChatTool.CreateFunctionTool(
            functionName: ToolName,
            functionDescription:
                "A persona drops a quick comment in the side-channel chat. Does NOT interrupt the presenter. "
                + "Use for '+1', short reactions, an aside, a clarification that doesn't need air time. "
                + "If you supply `text`, it is used verbatim; if omitted, the persona's voice service generates one.",
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

        var text = await ResolveText(arguments, personaId, context, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(text))
        {
            await Console.Error.WriteLineAsync($"[{ToolName}] empty text for '{personaId}'").ConfigureAwait(false);
            return;
        }

        var body = new JsonObject
        {
            ["kind"] = "chat",
            ["personaId"] = personaId,
            ["text"] = text,
        };
        await _poster.PostEvent(body, cancellationToken).ConfigureAwait(false);
    }

    private async Task<string?> ResolveText(
        JsonElement arguments,
        string personaId,
        ModeratorContext context,
        CancellationToken cancellationToken)
    {
        if (arguments.TryGetProperty("text", out var textEl) && textEl.GetString() is { Length: > 0 } supplied)
        {
            return supplied;
        }
        var previousLines = context.PersonaPreviousLines.TryGetValue(personaId, out var lines)
            ? lines
            : (IReadOnlyList<string>)[];
        return await _voice
            .GenerateLine(personaId, context.PresenterLine, context.RecentChunks, previousLines, cancellationToken)
            .ConfigureAwait(false);
    }

    private static BinaryData BuildSchema(IReadOnlyList<Persona> roster)
    {
        var personaEnum = "[" + string.Join(",", roster
            .Where(p => p.Archetype != Archetype.User)
            .Select(p => "\"" + p.Id + "\"")) + "]";
        return BinaryData.FromString($$"""
            {
              "type": "object",
              "properties": {
                "persona_id": { "type": "string", "enum": {{personaEnum}} },
                "text":       { "type": "string", "description": "Optional verbatim text. Omit to have the persona's voice service generate one." }
              },
              "required": ["persona_id"],
              "additionalProperties": false
            }
            """);
    }
}
