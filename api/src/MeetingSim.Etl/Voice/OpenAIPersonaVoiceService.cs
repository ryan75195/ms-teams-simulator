using System.ClientModel;
using MeetingSim.Core.Personas;
using MeetingSim.Etl.Voice.Interfaces;
using OpenAI.Chat;

namespace MeetingSim.Etl.Voice;

internal sealed class OpenAIPersonaVoiceService : IPersonaVoiceService
{
    public const string DefaultModelName = "gpt-4o-mini";
    public const int MaxLineLength = 350;

    private const int RecentChunkLimit = 4;
    private const int PreviousLinesLimit = 3;

    private readonly ChatClient _client;
    private readonly Dictionary<string, Persona> _personasById;

    public OpenAIPersonaVoiceService(ChatClient client, IReadOnlyList<Persona> roster)
    {
        _client = client;
        _personasById = roster.ToDictionary(p => p.Id, StringComparer.Ordinal);
    }

    public async Task<string> GenerateLine(
        string personaId,
        string presenterLine,
        IReadOnlyList<string> recentChunks,
        IReadOnlyList<string> personaPreviousLines,
        string? currentSlide = null,
        CancellationToken cancellationToken = default)
    {
        if (!_personasById.TryGetValue(personaId, out var persona))
        {
            throw new InvalidOperationException($"Unknown persona id '{personaId}'.");
        }

        var systemPrompt = BuildSystemPrompt(persona);
        var userPrompt = BuildUserPrompt(presenterLine, recentChunks, personaPreviousLines, currentSlide);

        var first = await Generate(systemPrompt, userPrompt, retryNote: null, cancellationToken).ConfigureAwait(false);
        var validation = ValidateLine(persona, first);
        if (validation is null)
        {
            return first;
        }
        await Console.Out.WriteLineAsync($"  [voice] {persona.Id}: {validation} — retrying").ConfigureAwait(false);
        var retried = await Generate(systemPrompt, userPrompt, retryNote: validation, cancellationToken).ConfigureAwait(false);
        return ValidateLine(persona, retried) is null ? retried : Truncate(retried, MaxLineLength);
    }

    private async Task<string> Generate(string systemPrompt, string userPrompt, string? retryNote, CancellationToken cancellationToken)
    {
        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(systemPrompt),
            new UserChatMessage(userPrompt),
        };
        if (retryNote is not null)
        {
            messages.Add(new SystemChatMessage($"Your previous attempt failed validation: {retryNote}. Try again — keep it short, no stage directions, no greetings."));
        }
        ClientResult<ChatCompletion> completion = await _client
            .CompleteChatAsync(messages, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return completion.Value.Content[0].Text.Trim();
    }

    internal static string? ValidateLine(Persona persona, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "empty response";
        }
        if (text.Length > MaxLineLength)
        {
            return $"too long ({text.Length} chars, max {MaxLineLength})";
        }
        if (text.Contains('*', StringComparison.Ordinal))
        {
            return "contains stage direction (*...*)";
        }
        var trimmed = text.TrimStart();
        if (trimmed.StartsWith("hi ", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("hello", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("hey ", StringComparison.OrdinalIgnoreCase))
        {
            return "starts with greeting";
        }
        var firstName = persona.Name.Split(' ')[0];
        if (trimmed.StartsWith($"as {firstName}", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith($"i'm {firstName}", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith($"i am {firstName}", StringComparison.OrdinalIgnoreCase))
        {
            return "self-introduces";
        }
        return null;
    }

    private static string Truncate(string text, int maxLength)
        => text.Length <= maxLength ? text : text[..maxLength].TrimEnd() + "…";

    internal static string BuildSystemPrompt(Persona persona)
    {
        var bioBlock = string.IsNullOrWhiteSpace(persona.Bio)
            ? string.Empty
            : $"\nWho you are: {persona.Bio}\n";

        return $"""
            You are {persona.Name}, a member of the audience at a presentation.
            {bioBlock}
            Default disposition: {DescribeArchetype(persona.Archetype)}

            Style:
            - Speak in the first person as {persona.Name}.
            - Keep replies to 1-2 sentences, conversational and natural — like you're actually in the meeting.
            - Don't introduce yourself. Don't use stage directions ("*leans forward*"). Don't preface with "As {persona.Name}, I think…".
            - Just say what you would say, exactly as you'd say it out loud.
            - Let your specific background colour the question — not every skeptic asks the same probe.
            """;
    }

    internal static string BuildUserPrompt(
        string presenterLine,
        IReadOnlyList<string> recentChunks,
        IReadOnlyList<string> personaPreviousLines,
        string? currentSlide = null)
    {
        var sections = new List<string>();

        if (!string.IsNullOrWhiteSpace(currentSlide))
        {
            sections.Add($"""
                Slide on screen (what you can see):
                {currentSlide}
                """);
        }

        var context = TakeTail(recentChunks, RecentChunkLimit);
        if (context.Count > 0)
        {
            var lines = string.Join("\n", context.Select(c => $"> {c}"));
            sections.Add($"""
                What the presenter has been saying:
                {lines}
                """);
        }

        var previousLines = TakeTail(personaPreviousLines, PreviousLinesLimit);
        if (previousLines.Count > 0)
        {
            var quoted = string.Join("\n", previousLines.Select(l => $"- \"{l}\""));
            sections.Add($"""
                What you've already said in this meeting:
                {quoted}
                """);
        }

        sections.Add($"""
            The presenter just said:
            > {presenterLine}

            Respond.
            """);

        return string.Join("\n\n", sections);
    }

    private static IReadOnlyList<string> TakeTail(IReadOnlyList<string> source, int limit)
        => source.Count > limit
            ? source.Skip(source.Count - limit).ToList()
            : source;

    internal static string DescribeArchetype(Archetype archetype) => archetype switch
    {
        Archetype.Skeptic => "You push back on numbers, forecasts, and unsupported claims. You care about ROI, assumptions, and whether the data actually supports the conclusion. Polite but probing.",
        Archetype.Curious => "You ask clarifying questions to understand things better. When something is glossed over you want to know more — what does that mean, how does that work, who's it for?",
        Archetype.Cheerleader => "You amplify wins and look for the upside. You ask \"how do we double down on this?\" and bring positive energy.",
        Archetype.Silent => "You rarely speak. When you do it's brief and observational — a small comment or short question, never a long monologue.",
        _ => "You're a thoughtful audience member.",
    };
}
