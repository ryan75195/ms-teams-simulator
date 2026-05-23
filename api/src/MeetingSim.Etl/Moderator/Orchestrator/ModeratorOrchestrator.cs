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

    public async Task<string> Decide(ModeratorContext context, CancellationToken cancellationToken = default)
    {
        var options = BuildOptions();
        var firstResult = await CallModel(context, options, retryNudge: null, cancellationToken)
            .ConfigureAwait(false);
        var finalResult = firstResult;

        var nudge = ValidateDecision(context, firstResult);
        if (nudge is not null)
        {
            await Console.Out.WriteLineAsync($"  [validation] {nudge}").ConfigureAwait(false);
            finalResult = await CallModel(context, options, nudge, cancellationToken)
                .ConfigureAwait(false);
        }

        await LogDecision(finalResult).ConfigureAwait(false);
        await PostDecision(context, finalResult, cancellationToken).ConfigureAwait(false);

        foreach (var call in finalResult.ToolCalls)
        {
            await _registry.Dispatch(call, context, cancellationToken).ConfigureAwait(false);
        }

        return SummariseDecision(context.Mode, finalResult);
    }

    private ChatCompletionOptions BuildOptions()
    {
        var options = new ChatCompletionOptions
        {
            ToolChoice = ChatToolChoice.CreateRequiredChoice(),
            AllowParallelToolCalls = true,
        };
        foreach (var definition in _registry.Definitions)
        {
            options.Tools.Add(definition);
        }
        return options;
    }

    private async Task<ChatCompletion> CallModel(
        ModeratorContext context,
        ChatCompletionOptions options,
        string? retryNudge,
        CancellationToken cancellationToken)
    {
        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(_systemPrompt),
            new UserChatMessage(BuildUserPrompt(context)),
        };
        if (retryNudge is not null)
        {
            messages.Add(new SystemChatMessage(retryNudge));
        }
        ClientResult<ChatCompletion> result = await _client
            .CompleteChatAsync(messages, options, cancellationToken)
            .ConfigureAwait(false);
        return result.Value;
    }

    internal static string? ValidateDecision(ModeratorContext context, ChatCompletion completion)
    {
        if (context.Mode != "complete" || string.IsNullOrEmpty(context.ActiveResponderId))
        {
            return null;
        }
        foreach (var call in completion.ToolCalls)
        {
            if (call.FunctionName == "cast_speak"
                && TryReadPersonaIdArg(call.FunctionArguments.ToString(), out var id)
                && id == context.ActiveResponderId)
            {
                return null;
            }
        }
        return $"Your previous decision did not cast_speak for the active responder '{context.ActiveResponderId}'. The presenter is in dialogue with them and just said: \"{context.PresenterLine}\". Decide again and cast_speak the active responder this turn unless the presenter explicitly addressed someone else.";
    }

    private static bool TryReadPersonaIdArg(string argsJson, out string personaId)
    {
        personaId = string.Empty;
        try
        {
            using var doc = JsonDocument.Parse(argsJson);
            if (doc.RootElement.TryGetProperty("persona_id", out var el) && el.GetString() is { Length: > 0 } id)
            {
                personaId = id;
                return true;
            }
        }
        catch (JsonException)
        {
        }
        return false;
    }

    private async Task PostDecision(ModeratorContext context, ChatCompletion completion, CancellationToken cancellationToken)
    {
        var decisionRecord = BuildDecisionRecord(context, completion);
        try
        {
            await _decisionPoster.PostDecision(decisionRecord, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            await Console.Error.WriteLineAsync($"[orchestrator] decision POST failed: {ex.Message}").ConfigureAwait(false);
        }
    }

    internal static string SummariseDecision(string mode, ChatCompletion completion)
    {
        var parts = completion.ToolCalls.Select(c =>
            TryReadPersonaIdArg(c.FunctionArguments.ToString(), out var id) && id.Length > 0
                ? $"{c.FunctionName}({id})"
                : c.FunctionName);
        var joined = string.Join(", ", parts);
        return string.IsNullOrEmpty(joined) ? $"{mode}: (no tools)" : $"{mode}: {joined}";
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

            Critical rule (do not violate): when an active responder is set AND the presenter's line is anything other than an explicit pivot or callout of another persona, that responder MUST cast_speak this turn. set_active_responder alone is not a reaction.

            Personas in the audience (only these may react):
            {personas}

            Three principles:

            1. Address routing & dialogue continuity. Identify who the presenter is addressing — including speech-to-text variance ("Brian" → Bryan, "Annie"/"Anich" → Anuj, "Ava" → Eva, "Cheryl" → Serena) — and cast_speak them. When an active responder is set, presenter follow-ups (questions, clarifications, pleasantries) MUST cast_speak that persona; clear with set_active_responder (no persona_id) only when the presenter explicitly names someone else or pivots topics. cast_speak sets the speaker as active responder by default.

            2. State hygiene. Lower stale hands on topic shifts. Don't repeat the same persona/text you've just used (recent decisions appear in the user prompt — vary). Reserve stay_quiet for genuinely contentless lines (throat-clearing, half-sentences, STT garbles).

            3. Engagement floor. Direct questions to the room ALWAYS get at least one reaction (send_chat confirmation or react 👍). Substantive presenter lines — questions, claims, transitions — get at least one reaction.

            Context modes — most turns are "complete". You may also receive:
            - "partial" — presenter is mid-sentence. cast_speak FORBIDDEN. Only react, raise_hand, lower_hand, send_chat, set_active_responder, or stay_quiet.
            - "silence" — presenter has paused. Quiet ambient only. cast_speak FORBIDDEN. Default to stay_quiet for short pauses; if interjecting, vary persona and phrasing across silences.
            - "slide" — presenter changed slides. React to the new content (reactions, raise_hand for questions, lower_hand for stale hands). cast_speak only for a persona with a strong reaction tied directly to this slide.

            You MUST call at least one tool. You MAY call multiple in parallel. Final reminder: if there is an active responder and the presenter's line is not a clear pivot, cast_speak them this turn — no exceptions.
            """;
    }

    internal static string BuildUserPrompt(ModeratorContext context)
    {
        var sections = new List<string>();

        AppendSlide(sections, context.CurrentSlide);
        AppendContext(sections, context.RecentChunks);
        AppendState(sections, context);
        AppendRecentDecisions(sections, context.RecentDecisions);
        sections.Add(BuildPresenterSection(context));

        return string.Join("\n\n", sections);
    }

    private static void AppendRecentDecisions(List<string> sections, IReadOnlyList<string>? recentDecisions)
    {
        if (recentDecisions is null || recentDecisions.Count == 0)
        {
            return;
        }
        var lines = string.Join("\n", recentDecisions.Select(d => $"- {d}"));
        sections.Add($"""
            Your recent decisions (don't repeat the same persona/text):
            {lines}
            """);
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
