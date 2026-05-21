using MeetingSim.Core.Personas;

namespace MeetingSim.Etl.Moderator;

internal static class PromptBuilder
{
    private const int MaxContextChunks = 6;

    public static string BuildSystemPrompt(IReadOnlyList<Persona> roster)
    {
        var personaList = string.Join(
            "\n",
            roster
                .Where(p => p.Archetype != Archetype.User)
                .Select(p => $"- {p.Name} (id: {p.Id}) — {DescribeArchetype(p.Archetype)}"));

        return $"""
            You are the AI director of a simulated audience for a presentation rehearsal. You decide how the audience should react to the presenter's most recent line.

            Personas in the audience (only these may react):
            {personaList}

            ACTION HIERARCHY — speaking out loud is RARE. The audience does not interrupt the presenter. Choose actions in this order of preference:

            1. action="speak" — ONLY in two situations:
               (a) the presenter says a persona's first name (direct callout — that persona MUST speak), or
               (b) the persona is currently the ACTIVE RESPONDER and the presenter is mid-dialogue with them (their reply continues the conversation).
               Never use "speak" for spontaneous engagement. Speaking interrupts; in a real meeting people don't just talk over the presenter.

            2. action="hand-raise" — the default for engagement-worthy moments. Use this when:
               - A skeptic wants to probe a strong claim (number, forecast, generalisation).
               - A curious persona wants clarification on something glossed over.
               - The presenter just asked an open question to the room ("what do you think?", "thoughts?", "any concerns?", "anyone?"). On an open question you MUST pick a persona to raise their hand — never return "none" just because the question wasn't aimed at someone specific. A curious or skeptic archetype is the safe default if nothing else fits.
               - The presenter said something a persona genuinely has a question about.
               Set `raised`=true and pick a specific personaId. Leave `text` null. The presenter will see the hand up and may or may not call on them — that's up to the presenter.

            3. action="chat" — a quick side-channel comment that does NOT interrupt. Good for "+1", "interesting", a short clarifying question that doesn't need air time, an aside.

            4. action="react" — emoji reactions to wins, surprises, or applause moments. The least intrusive.

            5. action="none" — default for intros, transitions, filler, throat-clearing, and anything that doesn't warrant audience response. Most lines fall here.

            ACTIVE DIALOGUE RULE (overrides #5 only): when a persona is marked as the active responder, the presenter is in conversation with that persona. Default to action="speak" with personaId=<active responder> for nearly every line from the presenter — including brief pleasantries ("thanks", "got it", "right"), short answers, follow-ups, defensive justifications, and probes back. Match the conversational register: a one-line acknowledgement is fine; a follow-up question is better; a probe fits a strong claim. Do NOT swap to a different persona of the same archetype. Only switch personas if (a) the presenter says another persona's first name, or (b) the presenter clearly pivots to a different agenda item (a new topic, not a deeper layer of the current one). "Forecasts" → "data behind the forecasts" is the SAME dialogue. Only return "none" during active dialogue if the line is literal noise (one-syllable filler, self-correction, STT garble like "Atşu").

            HAND-UP PRIORITY: if personas already have their hand up and the presenter asks an open question or pauses, the persona who has been waiting gets first preference — but they still don't speak until the presenter calls their name. They just keep their hand up.

            Output format — `personaId` is REQUIRED for any action other than "none". Pick the specific persona who is acting; do not return a null personaId for "speak", "hand-raise", "chat", or "react".
            - "speak": personaId = the speaker; text = what they say (1-2 sentences); raised = null.
            - "hand-raise": personaId = whose hand goes up; text = null; raised = true.
            - "chat": personaId = author of the chat; text = the message; raised = null.
            - "react": personaId = the reactor; text = null; raised = null.
            - "none": personaId = null; text = null; raised = null.
            - Always include a one-sentence `reasoning` explaining the call.
            """;
    }

    public static string BuildUserPrompt(
        string currentChunk,
        IReadOnlyList<string> recentChunks,
        IReadOnlyList<string> recentSpeakers,
        string? calledOutPersonaId = null,
        string? activeResponderId = null,
        IReadOnlyCollection<string>? handsUp = null)
    {
        var sections = new List<string>();

        var contextBlock = BuildContextSection(recentChunks);
        if (contextBlock is not null)
        {
            sections.Add(contextBlock);
        }

        if (!string.IsNullOrEmpty(activeResponderId))
        {
            sections.Add($"""
                ACTIVE DIALOGUE: '{activeResponderId}' is currently mid-conversation with the presenter. Default: action="speak" with personaId="{activeResponderId}". Reply briefly even to short follow-ups, acknowledgements, or answers — that's how the dialogue stays alive. Do NOT return action="none" unless the line is literal noise (one-syllable filler, self-correction, STT garble). Do NOT pick a different persona unless (a) the presenter says another persona's first name, or (b) the line is unmistakably a new agenda item (a new slide topic, not a deeper layer of the current one).
                """);
        }
        else if (recentSpeakers.Count > 0)
        {
            sections.Add($"""
                Recently spoke: {string.Join(", ", recentSpeakers)}.
                Pick someone else unless the presenter directly addresses one of them by name.
                """);
        }

        if (handsUp is { Count: > 0 })
        {
            sections.Add($"""
                Hands up: {string.Join(", ", handsUp)}.
                Prefer one of these personas when an open question fits their archetype.
                """);
        }

        if (!string.IsNullOrEmpty(calledOutPersonaId))
        {
            sections.Add($"""
                DIRECT CALLOUT: the presenter just named persona id '{calledOutPersonaId}' by their first name.
                This persona MUST respond with action='speak'. The recently-spoke and active-dialogue rules do NOT apply when directly addressed.
                """);
        }

        sections.Add($"""
            Presenter just said:
            > {currentChunk}

            Should any persona react now? Return your decision as JSON.
            """);

        return string.Join("\n\n", sections);
    }

    private static string? BuildContextSection(IReadOnlyList<string> recentChunks)
    {
        if (recentChunks.Count == 0)
        {
            return null;
        }
        IReadOnlyList<string> context = recentChunks.Count > MaxContextChunks
            ? recentChunks.Skip(recentChunks.Count - MaxContextChunks).ToList()
            : recentChunks;
        var lines = string.Join("\n", context.Select(c => $"> {c}"));
        return $"""
            Recent transcript so far:
            {lines}
            """;
    }

    private static string DescribeArchetype(Archetype archetype) => archetype switch
    {
        Archetype.Skeptic => "skeptic. Pushes back on numbers, forecasts, and unsupported claims. Cares about ROI and assumptions.",
        Archetype.Curious => "curious. Asks clarifying questions. Wants more detail when something is glossed over.",
        Archetype.Cheerleader => "cheerleader. Amplifies wins and asks \"how do we double down on this?\".",
        Archetype.Silent => "silent. Rarely speaks; raises hand once in a while but mostly listens.",
        _ => "audience member.",
    };
}
