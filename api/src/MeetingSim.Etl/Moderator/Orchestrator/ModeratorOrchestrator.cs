using System.ClientModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using MeetingSim.Core.Personas;
using MeetingSim.Etl.Moderator.Interfaces;
using OpenAI.Chat;

namespace MeetingSim.Etl.Moderator.Orchestrator;

internal sealed class ModeratorOrchestrator
{
    private readonly ChatClient _client;
    private readonly ModeratorToolRegistry _registry;
    private readonly IDecisionPoster _decisionPoster;
    private readonly string _systemPrompt;

    public ModeratorOrchestrator(
        ChatClient client,
        ModeratorToolRegistry registry,
        IDecisionPoster decisionPoster,
        IReadOnlyList<Persona> roster)
    {
        _client = client;
        _registry = registry;
        _decisionPoster = decisionPoster;
        _systemPrompt = BuildSystemPrompt(roster);
    }

    public async Task Decide(ModeratorContext context, CancellationToken cancellationToken = default)
    {
        var messages = new ChatMessage[]
        {
            new SystemChatMessage(_systemPrompt),
            new UserChatMessage(BuildUserPrompt(context)),
        };

        var options = new ChatCompletionOptions
        {
            ToolChoice = ChatToolChoice.CreateRequiredChoice(),
            AllowParallelToolCalls = true,
        };
        foreach (var definition in _registry.Definitions)
        {
            options.Tools.Add(definition);
        }

        ClientResult<ChatCompletion> result = await _client
            .CompleteChatAsync(messages, options, cancellationToken)
            .ConfigureAwait(false);

        await LogDecision(result.Value).ConfigureAwait(false);

        var decisionRecord = BuildDecisionRecord(context, result.Value);
        try
        {
            await _decisionPoster.PostDecision(decisionRecord, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            await Console.Error.WriteLineAsync($"[orchestrator] decision POST failed: {ex.Message}").ConfigureAwait(false);
        }

        foreach (var call in result.Value.ToolCalls)
        {
            await _registry.Dispatch(call, context, cancellationToken).ConfigureAwait(false);
        }
    }

    private static JsonObject BuildDecisionRecord(ModeratorContext context, ChatCompletion completion)
    {
        var toolCalls = new JsonArray();
        foreach (var call in completion.ToolCalls)
        {
            JsonNode? args = null;
            try
            {
                args = JsonNode.Parse(call.FunctionArguments.ToString());
            }
            catch (JsonException)
            {
                args = JsonValue.Create(call.FunctionArguments.ToString());
            }
            toolCalls.Add(new JsonObject
            {
                ["name"] = call.FunctionName,
                ["args"] = args,
            });
        }

        return new JsonObject
        {
            ["ts"] = DateTimeOffset.UtcNow.ToString("O"),
            ["presenterLine"] = context.PresenterLine,
            ["state"] = new JsonObject
            {
                ["activeResponderId"] = context.ActiveResponderId,
                ["handsUp"] = new JsonArray(context.HandsUp.Select(h => (JsonNode?)JsonValue.Create(h)).ToArray()),
                ["recentSpeakers"] = new JsonArray(context.RecentSpeakers.Select(s => (JsonNode?)JsonValue.Create(s)).ToArray()),
                ["hasSlide"] = !string.IsNullOrWhiteSpace(context.CurrentSlide),
            },
            ["reasoning"] = completion.Content.Count > 0 ? completion.Content[0].Text : null,
            ["toolCalls"] = toolCalls,
        };
    }

    internal static string BuildSystemPrompt(IReadOnlyList<Persona> roster)
    {
        var personas = string.Join(
            "\n",
            roster
                .Where(p => p.Archetype != Archetype.User)
                .Select(p => $"- {p.Name} (id: {p.Id}) — {DescribeArchetype(p.Archetype)}{FormatBio(p.Bio)}"));

        return $"""
            You are the audience director for a presentation simulator. The presenter just said something; decide how the audience reacts by calling one tool per persona who reacts. You also own the room's state — who is the active responder, whose hand is still relevant.

            Personas in the audience (only these may react):
            {personas}

            Each tool's description tells you when to use it. These principles override everything else:

            1. Speaking out loud (cast_speak) interrupts the presenter. Reserve it for personas the presenter is directly addressing, and for active-responder continuations. If a persona wants to engage but wasn't addressed, they raise_hand and wait.
            2. Inferring the addressed persona is your job. If the presenter says a name that looks like one of the personas above — allowing for speech-to-text variance ("Brian" → Bryan, "Annie"/"Anich" → Anuj, "Ava" → Eva, "Cheryl" → Serena) — they are addressing that persona. cast_speak for them.
            3. The active responder owns dialogue continuity. When an active responder is set AND the presenter's line is a question, request, clarification, or pleasantry, you MUST cast_speak that persona this turn. They have to actually respond — set_active_responder alone is not a reaction. Only stop addressing the active responder when the presenter explicitly names a different persona or pivots topics; then call set_active_responder with no persona_id to clear. cast_speak sets the speaker as active responder by default — only call set_active_responder to override.
            4. Hands go stale. If a persona's hand has been up across multiple turns and the topic has shifted, call lower_hand to bring it down. Don't let hands accumulate.
            5. Direct questions to the room ALWAYS get a response, even small ones. "Can anyone hear me?", "is this working?", "ready?" — at least one persona should send_chat a confirmation or react with 👍. NEVER return only stay_quiet when the presenter asked the room a yes/no or check-in question.

            Reserve stay_quiet for genuinely contentless lines: throat-clearing ("um", "OK so"), half-sentences, obvious STT garbles. A substantive line — a question, a claim, a transition — should get at least one reaction.

            Context modes — most turns are "complete" (presenter just finished a thought). You may also receive:
            - "partial" — presenter is mid-sentence. cast_speak is FORBIDDEN (would cut them off). Only react, raise_hand, lower_hand, send_chat, set_active_responder, or stay_quiet.
            - "silence" — presenter has paused. Quiet ambient reactions only. cast_speak is FORBIDDEN — don't break the silence with audio. A curious persona may send_chat to fill, or stay_quiet is fine.
            - "slide" — presenter changed slides. React to the new content (👍/🤔, raise_hand for questions, lower_hand for stale hands). cast_speak only if a persona has a strong reaction tied directly to the new slide.

            You MUST call at least one tool. You MAY call multiple tools in parallel.
            """;
    }

    internal static string BuildUserPrompt(ModeratorContext context)
    {
        var sections = new List<string>();

        AppendSlide(sections, context.CurrentSlide);
        AppendContext(sections, context.RecentChunks);
        AppendState(sections, context);
        sections.Add(BuildPresenterSection(context));

        return string.Join("\n\n", sections);
    }

    private static string BuildPresenterSection(ModeratorContext context) => context.Mode switch
    {
        "partial" => $"""
            MODE: partial. Presenter is mid-sentence (transcript so far):
            > {context.PresenterLine}

            Do NOT cast_speak — you'd cut them off. Use react, raise_hand, send_chat, lower_hand, or stay_quiet.
            """,
        "silence" => """
            MODE: silence. Presenter has paused — no speech right now.

            Quiet ambient only. cast_speak FORBIDDEN. Default to stay_quiet for short pauses. If interjecting, vary it: a thinking emoji from a curious persona, a brief encouraging chat from a cheerleader, a quick clarifying chat from a skeptic. Don't reach for the same persona or the same phrasing each silence — silences are rare; treat each as a fresh moment, not a template fill.
            """,
        "slide" => """
            MODE: slide. Presenter just changed slides. Audience reads the new content above.

            React to the slide: 👍/🤔, raise_hand for questions, lower_hand for stale hands. cast_speak only for a persona with a strong reaction tied directly to this slide.
            """,
        _ => $"""
            Presenter just said:
            > {context.PresenterLine}

            Decide which tools to call.
            """,
    };

    private static void AppendSlide(List<string> sections, string? slide)
    {
        if (string.IsNullOrWhiteSpace(slide))
        {
            return;
        }
        sections.Add($"""
            Slide on screen (what the audience can see):
            {slide}
            """);
    }

    private static void AppendContext(List<string> sections, IReadOnlyList<string> recentChunks)
    {
        if (recentChunks.Count == 0)
        {
            return;
        }
        var tail = recentChunks.Count > 6
            ? recentChunks.Skip(recentChunks.Count - 6).ToList()
            : recentChunks;
        var lines = string.Join("\n", tail.Select(c => $"> {c}"));
        sections.Add($"""
            Recent transcript:
            {lines}
            """);
    }

    private static void AppendState(List<string> sections, ModeratorContext context)
    {
        var lines = new List<string>();
        if (!string.IsNullOrEmpty(context.ActiveResponderId))
        {
            lines.Add($"Active responder (locked in dialogue with presenter): {context.ActiveResponderId}");
        }
        if (context.HandsUp.Count > 0)
        {
            lines.Add($"Hands up (waiting to be addressed by name): {string.Join(", ", context.HandsUp)}");
        }
        if (context.RecentSpeakers.Count > 0)
        {
            lines.Add($"Recently spoke: {string.Join(", ", context.RecentSpeakers)}");
        }
        if (lines.Count == 0)
        {
            return;
        }
        sections.Add(string.Join("\n", lines));
    }

    private static async Task LogDecision(ChatCompletion completion)
    {
        if (completion.Content.Count > 0 && !string.IsNullOrWhiteSpace(completion.Content[0].Text))
        {
            await Console.Out.WriteLineAsync($"  reasoning: {completion.Content[0].Text}").ConfigureAwait(false);
        }
        foreach (var call in completion.ToolCalls)
        {
            await Console.Out.WriteLineAsync($"  tool     : {call.FunctionName} {call.FunctionArguments}").ConfigureAwait(false);
        }
    }

    private static string DescribeArchetype(Archetype archetype) => archetype switch
    {
        Archetype.Skeptic => "skeptic. Probes numbers and assumptions.",
        Archetype.Curious => "curious. Asks clarifying questions.",
        Archetype.Cheerleader => "cheerleader. Amplifies wins.",
        Archetype.Silent => "silent. Mostly listens.",
        _ => "audience member.",
    };

    private static string FormatBio(string? bio)
        => string.IsNullOrWhiteSpace(bio) ? string.Empty : $" {bio}";
}
