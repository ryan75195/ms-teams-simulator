using System.Text;
using MeetingSim.Core.Personas;

namespace MeetingSim.Etl.Moderator;

internal static class PromptBuilder
{
    private const int MaxContextChunks = 6;

    public static string BuildSystemPrompt(IReadOnlyList<Persona> roster)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are the AI director of a simulated audience for a presentation rehearsal.");
        sb.AppendLine();
        sb.AppendLine("Personas in the audience (only these may react):");
        foreach (var persona in roster)
        {
            if (persona.Archetype == Archetype.User)
            {
                continue;
            }

            sb.Append("- ");
            sb.Append(persona.Name);
            sb.Append(" (id: ");
            sb.Append(persona.Id);
            sb.Append(") — ");
            sb.AppendLine(DescribeArchetype(persona.Archetype));
        }
        sb.AppendLine();
        sb.AppendLine("Your job: decide whether any persona should react to the presenter's most recent line.");
        sb.AppendLine();
        sb.AppendLine("Most lines need no reaction (action = \"none\"). Only react when the moment genuinely warrants it:");
        sb.AppendLine("- The presenter directly names a persona (\"Anuj, you had a question?\") → that persona MUST react with action = \"speak\".");
        sb.AppendLine("- A strong claim a skeptic would probe (a number, a forecast, a generalisation).");
        sb.AppendLine("- A clarifying need a curious persona would surface.");
        sb.AppendLine("- A win moment a cheerleader would amplify.");
        sb.AppendLine();
        sb.AppendLine("Stay quiet on intros, transitions, throat-clearing, or anything that wouldn't naturally provoke a reply in a real meeting.");
        sb.AppendLine();
        sb.AppendLine("When you do react:");
        sb.AppendLine("- Pick exactly one persona whose archetype best fits the moment.");
        sb.AppendLine("- Generate `text` in that persona's voice (one or two sentences max).");
        sb.AppendLine("- For \"speak\" / \"chat\": include the actual question or comment in `text`. Leave `raised` null.");
        sb.AppendLine("- For \"hand-raise\": set `raised` to true and leave `text` null.");
        sb.AppendLine("- For \"none\": set `personaId`, `text`, and `raised` to null.");
        sb.AppendLine("- Always include a one-sentence `reasoning` explaining the call.");
        return sb.ToString();
    }

    public static string BuildUserPrompt(
        string currentChunk,
        IReadOnlyList<string> recentChunks,
        IReadOnlyList<string> recentSpeakers,
        string? calledOutPersonaId = null)
    {
        var sb = new StringBuilder();
        IReadOnlyList<string> context = recentChunks.Count > MaxContextChunks
            ? recentChunks.Skip(recentChunks.Count - MaxContextChunks).ToList()
            : recentChunks;

        if (context.Count > 0)
        {
            sb.AppendLine("Recent transcript so far:");
            foreach (var chunk in context)
            {
                sb.Append("> ");
                sb.AppendLine(chunk);
            }
            sb.AppendLine();
        }

        if (recentSpeakers.Count > 0)
        {
            sb.Append("Recently spoke: ");
            sb.Append(string.Join(", ", recentSpeakers));
            sb.AppendLine(".");
            sb.AppendLine("Pick someone else unless the presenter directly addresses one of them by name.");
            sb.AppendLine();
        }

        if (!string.IsNullOrEmpty(calledOutPersonaId))
        {
            sb.Append("DIRECT CALLOUT: the presenter just named persona id '");
            sb.Append(calledOutPersonaId);
            sb.AppendLine("' by their first name.");
            sb.AppendLine("This persona MUST respond with action='speak'. The recently-spoke suppression does NOT apply when directly addressed.");
            sb.AppendLine();
        }

        sb.AppendLine("Presenter just said:");
        sb.Append("> ");
        sb.AppendLine(currentChunk);
        sb.AppendLine();
        sb.Append("Should any persona react now? Return your decision as JSON.");
        return sb.ToString();
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
