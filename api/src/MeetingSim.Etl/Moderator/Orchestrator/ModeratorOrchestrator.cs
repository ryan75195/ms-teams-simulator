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
                ["calledOutPersonaId"] = context.CalledOutPersonaId,
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
                .Select(p => $"- {p.Name} (id: {p.Id}) — {DescribeArchetype(p.Archetype)}"));

        return $"""
            You are the audience director for a presentation simulator. The presenter just said something; decide how the audience reacts by calling one tool per persona who reacts.

            Personas in the audience (only these may react):
            {personas}

            Each tool's description tells you when to use it. Three principles override everything else:

            1. Speaking out loud interrupts the presenter — reserve `cast_speak` for direct name callouts and active-dialogue continuations only. If a persona wants to engage, they raise their hand and wait.
            2. The active responder (if set) owns the dialogue. Every follow-up from the presenter — pleasantries, answers, clarifying questions — keeps going to them until the presenter names someone else or pivots to a brand-new topic.
            3. Direct questions to the room ALWAYS get a response, even small ones. "Can anyone hear me?", "is this working?", "are we good to start?", "ready?" — at least one persona should `send_chat` a quick confirmation ("Hearing you fine" / "All good") or `react` with 👍. NEVER return only `stay_quiet` when the presenter asked the room a yes/no or check-in question.

            Reserve `stay_quiet` for genuinely contentless lines: throat-clearing ("um", "OK so"), half-sentences, obvious STT garbles. A substantive line from the presenter — a question, a claim, a transition between agenda items — should get at least one reaction (hand-raise, chat, or react).

            You MUST call at least one tool. You MAY call multiple tools in parallel — for example, one persona raising their hand while another reacts with an emoji.
            """;
    }

    internal static string BuildUserPrompt(ModeratorContext context)
    {
        var sections = new List<string>();

        AppendSlide(sections, context.CurrentSlide);
        AppendContext(sections, context.RecentChunks);
        AppendState(sections, context);

        sections.Add($"""
            Presenter just said:
            > {context.PresenterLine}

            Decide which tools to call.
            """);

        return string.Join("\n\n", sections);
    }

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
        if (!string.IsNullOrEmpty(context.CalledOutPersonaId))
        {
            lines.Add($"DIRECT CALLOUT: the presenter named '{context.CalledOutPersonaId}' by their first name — cast_speak for them is mandatory this turn.");
        }
        if (!string.IsNullOrEmpty(context.ActiveResponderId))
        {
            lines.Add($"Active responder (in dialogue with presenter): {context.ActiveResponderId}");
        }
        if (context.HandsUp.Count > 0)
        {
            lines.Add($"Hands up (waiting to be called on by name): {string.Join(", ", context.HandsUp)}");
        }
        if (context.RecentSpeakers.Count > 0)
        {
            lines.Add($"Recently spoke (prefer variety unless they're the active responder): {string.Join(", ", context.RecentSpeakers)}");
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
}
