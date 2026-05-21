using System.Text.Json;
using System.Text.Json.Nodes;
using MeetingSim.Core.Personas;
using MeetingSim.Etl.Moderator.Interfaces;
using MeetingSim.Etl.Moderator.Orchestrator.Interfaces;
using MeetingSim.Etl.Voice.Interfaces;
using OpenAI.Chat;

namespace MeetingSim.Etl.Moderator.Orchestrator.Tools;

internal sealed class CastSpeakTool : IModeratorTool
{
    public const string ToolName = "cast_speak";
    public const int DefaultSpeakDurationMs = 3000;

    private readonly IEventPoster _poster;
    private readonly IPersonaVoiceService _voice;
    private readonly ChatTool _definition;

    public CastSpeakTool(
        IReadOnlyList<Persona> roster,
        IEventPoster poster,
        IPersonaVoiceService voice)
    {
        _poster = poster;
        _voice = voice;
        _definition = ChatTool.CreateFunctionTool(
            functionName: ToolName,
            functionDescription:
                "A persona speaks out loud — an audible interruption. Use ONLY when the presenter has just "
                + "named the persona by first name (direct callout), or when this persona is the active "
                + "responder and the presenter is mid-dialogue with them. The persona's voice service "
                + "generates the actual line based on recent context.",
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

        var previousLines = context.PersonaPreviousLines.TryGetValue(personaId, out var lines)
            ? lines
            : (IReadOnlyList<string>)[];

        var text = await _voice
            .GenerateLine(personaId, context.PresenterLine, context.RecentChunks, previousLines, cancellationToken)
            .ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(text))
        {
            await Console.Error.WriteLineAsync($"[{ToolName}] voice service returned empty text for '{personaId}'")
                .ConfigureAwait(false);
            return;
        }

        var speakBody = new JsonObject
        {
            ["kind"] = "speak",
            ["personaId"] = personaId,
            ["text"] = text,
            ["durationMs"] = DefaultSpeakDurationMs,
        };
        await _poster.PostEvent(speakBody, cancellationToken).ConfigureAwait(false);

        if (context.HandsUp.Contains(personaId))
        {
            var lowerBody = new JsonObject
            {
                ["kind"] = "hand-raise",
                ["personaId"] = personaId,
                ["raised"] = false,
            };
            await _poster.PostEvent(lowerBody, cancellationToken).ConfigureAwait(false);
        }
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
                "persona_id": { "type": "string", "enum": {{personaEnum}} }
              },
              "required": ["persona_id"],
              "additionalProperties": false
            }
            """);
    }
}
