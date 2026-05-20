using System.Globalization;
using MeetingSim.Core.Personas.Interfaces;

namespace MeetingSim.Core.Personas;

public sealed class CrowdService : ICrowdService
{
    public const string CrowdIdPrefix = "crowd:";

    private static readonly IReadOnlyList<string> FirstNames =
    [
        "Maya", "Theo", "Priya", "Marcus", "Lena", "Diego", "Aisha", "Owen",
        "Yuki", "Felix", "Nadia", "Liam", "Sofia", "Jin", "Elena", "Tariq",
        "Hana", "Noor", "Sebastian", "Mei", "Jonas", "Amara", "Hugo", "Zara",
        "Casey", "Tomás", "Ines", "Rohan", "Wren", "Eitan",
    ];

    private static readonly IReadOnlyList<string> LastNames =
    [
        "Rodriguez", "Pereira", "Hossain", "Ng", "Andersson", "Cohen", "Bernal",
        "Wei", "Singh", "Patel", "Lindqvist", "Costa", "Adebayo", "Park",
        "Howard", "O'Connor", "Schmidt", "Hassan", "Bauer", "Vasquez", "Petrov",
        "Kim", "Larsen", "Yamamoto", "Ali", "Okafor", "Holm", "Reyes", "Iqbal",
        "Quan",
    ];

    private static readonly IReadOnlyList<string> Palette =
    [
        "#7A4F8B", "#3F628E", "#6E5BC5", "#A05E5E", "#3C7A6B", "#8A6234",
        "#5E7F3E", "#A06544", "#4F8295", "#7E4F8B", "#6B8A52", "#9A6B38",
        "#557A46", "#915C83", "#3A7CA5", "#8A3480",
    ];

    public Persona Generate(int seed, int index)
    {
        var rng = SeedFor(seed, index);
        var firstName = FirstNames[(int)(NextUInt(ref rng) % (uint)FirstNames.Count)];
        var lastName = LastNames[(int)(NextUInt(ref rng) % (uint)LastNames.Count)];
        var color = Palette[(int)(NextUInt(ref rng) % (uint)Palette.Count)];

        return new Persona(
            Id: FormatCrowdId(seed, index),
            Name: $"{firstName} {lastName}",
            Color: color,
            Archetype: Archetype.Unknown,
            IsRoster: false);
    }

    public bool TryParseCrowdId(string personaId, out int seed, out int index)
    {
        seed = 0;
        index = 0;

        if (string.IsNullOrEmpty(personaId) || !personaId.StartsWith(CrowdIdPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        var parts = personaId.Split(':');
        if (parts.Length != 3)
        {
            return false;
        }

        return int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out seed)
            && int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out index);
    }

    private static string FormatCrowdId(int seed, int index)
        => string.Create(
            CultureInfo.InvariantCulture,
            $"{CrowdIdPrefix}{seed}:{index}");

    private static uint SeedFor(int seed, int index)
        => unchecked((uint)(seed * 0x9301) + (uint)(index * 0xC0399));

    private static uint NextUInt(ref uint state)
    {
        unchecked
        {
            state += 0x6D2B79F5u;
            var z = state;
            z = (z ^ (z >> 15)) * (z | 1u);
            z ^= z + (z ^ (z >> 7)) * (z | 61u);
            return z ^ (z >> 14);
        }
    }
}
