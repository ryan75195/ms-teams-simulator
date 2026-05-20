using MeetingSim.Core.Personas.Interfaces;

namespace MeetingSim.Core.Personas;

public sealed class PersonaRepository : IPersonaRepository
{
    private static readonly IReadOnlyList<Persona> DefaultRoster =
    [
        new("you",       "Ryan Khan",         "#7A4F8B", Archetype.User,        IsRoster: true),
        new("anuj",      "Anuj Kapoor",       "#3F628E", Archetype.Skeptic,     IsRoster: true),
        new("serena",    "Serena Davis",      "#6E5BC5", Archetype.Curious,     IsRoster: true),
        new("kayo",      "Kayo Miwa",         "#3C7A6B", Archetype.Cheerleader, IsRoster: true),
        new("isaac",     "Isaac Summers",     "#A05E5E", Archetype.Silent,      IsRoster: true),
        new("charlotte", "Charlotte de Crum", "#557A46", Archetype.Curious,     IsRoster: true),
        new("danielle",  "Danielle Booker",   "#8A6234", Archetype.Skeptic,     IsRoster: true),
        new("ray",       "Ray Tanaka",        "#5E7F3E", Archetype.Cheerleader, IsRoster: true),
        new("bryan",     "Bryan Wright",      "#A06544", Archetype.Curious,     IsRoster: true),
        new("eva",       "Eva Terrazas",      "#4F8295", Archetype.Skeptic,     IsRoster: true),
        new("krystal",   "Krystal McMurray",  "#9A6B38", Archetype.Cheerleader, IsRoster: true),
        new("alvin",     "Alvin Tao",         "#3A7CA5", Archetype.Silent,      IsRoster: true),
    ];

    private readonly ICrowdService _crowd;
    private readonly Dictionary<string, Persona> _rosterById;

    public PersonaRepository(ICrowdService crowd)
    {
        _crowd = crowd;
        _rosterById = DefaultRoster.ToDictionary(p => p.Id, StringComparer.Ordinal);
    }

    public IReadOnlyList<Persona> Roster => DefaultRoster;

    public Persona? Resolve(string personaId)
    {
        if (string.IsNullOrEmpty(personaId))
        {
            return null;
        }

        if (_rosterById.TryGetValue(personaId, out var rosterPersona))
        {
            return rosterPersona;
        }

        if (_crowd.TryParseCrowdId(personaId, out var seed, out var index))
        {
            return _crowd.Generate(seed, index);
        }

        return null;
    }

    public IReadOnlyList<Persona> Crowd(int seed, int skip, int count)
    {
        if (count <= 0)
        {
            return [];
        }

        var safeSkip = Math.Max(0, skip);
        var safeCount = Math.Min(count, 1000);
        var list = new List<Persona>(safeCount);
        for (var i = 0; i < safeCount; i++)
        {
            list.Add(_crowd.Generate(seed, safeSkip + i));
        }
        return list;
    }
}
