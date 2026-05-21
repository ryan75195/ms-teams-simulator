using System.Text.RegularExpressions;
using MeetingSim.Core.Personas;

namespace MeetingSim.Etl.Moderator;

internal static class CalloutDetector
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(50);

    public static string? Detect(string transcript, IReadOnlyList<Persona> roster)
    {
        if (string.IsNullOrWhiteSpace(transcript))
        {
            return null;
        }

        foreach (var persona in roster)
        {
            if (persona.Archetype == Archetype.User)
            {
                continue;
            }

            var firstName = ExtractFirstName(persona.Name);
            if (string.IsNullOrEmpty(firstName))
            {
                continue;
            }

            var pattern = $@"\b{Regex.Escape(firstName)}\b";
            if (Regex.IsMatch(transcript, pattern, RegexOptions.IgnoreCase, RegexTimeout))
            {
                return persona.Id;
            }
        }

        return null;
    }

    private static string ExtractFirstName(string fullName)
    {
        var space = fullName.IndexOf(' ', StringComparison.Ordinal);
        return space > 0 ? fullName[..space] : fullName;
    }
}
