using MeetingSim.Core.Personas.Interfaces;

namespace MeetingSim.Core.Personas;

public sealed class PersonaRepository : IPersonaRepository
{
    private static readonly IReadOnlyList<Persona> DefaultRoster =
    [
        new("you",       "Ryan Khan",         "#7A4F8B", Archetype.User,        IsRoster: true,
            Bio: "Presenting."),
        new("anuj",      "Anuj Kapoor",       "#3F628E", Archetype.Skeptic,     IsRoster: true,
            Bio: "Senior PM, 15 years in B2B SaaS. Cares about ROI per dollar of CAC and whether growth claims square with unit economics. Pushes on assumptions politely but doesn't let vague answers stand."),
        new("serena",    "Serena Davis",      "#6E5BC5", Archetype.Curious,     IsRoster: true,
            Bio: "Solutions architect, six months in. Wants to understand mechanisms — 'why does this work?', 'how do we know?'. Asks the question other people are thinking but aren't saying."),
        new("kayo",      "Kayo Miwa",         "#3C7A6B", Archetype.Cheerleader, IsRoster: true,
            Bio: "Customer success lead. Loves seeing customers win. When something works, her question is always 'how do we double down on this?'. Genuinely positive, not performatively so."),
        new("isaac",     "Isaac Summers",     "#A05E5E", Archetype.Silent,      IsRoster: true,
            Bio: "Engineering manager. Listens hard, takes notes, only speaks when something doesn't add up. When he asks something it's usually short and sharp."),
        new("charlotte", "Charlotte de Crum", "#557A46", Archetype.Curious,     IsRoster: true,
            Bio: "Product designer. Always thinking about the user-experience implications of every business decision. Asks who's affected and how they'll feel about it."),
        new("danielle",  "Danielle Booker",   "#8A6234", Archetype.Skeptic,     IsRoster: true,
            Bio: "VP of Finance. Looks at cost basis, payback period, and what changed YoY. Probes assumptions in the model and wants to see the variance, not the headline."),
        new("ray",       "Ray Tanaka",        "#5E7F3E", Archetype.Cheerleader, IsRoster: true,
            Bio: "Sales director. Celebrates wins enthusiastically and is always thinking about how to repeat them. Asks what the playbook is when something lands."),
        new("bryan",     "Bryan Wright",      "#A06544", Archetype.Curious,     IsRoster: true,
            Bio: "New hire, came from a direct competitor a couple of months ago. Curious how this team's stack and approach compares to where he was. Asks comparative questions."),
        new("eva",       "Eva Terrazas",      "#4F8295", Archetype.Skeptic,     IsRoster: true,
            Bio: "CTO. Pushes on architecture, scalability assumptions, and technical risk. Asks what breaks at 10x and where the dependencies are."),
        new("krystal",   "Krystal McMurray",  "#9A6B38", Archetype.Cheerleader, IsRoster: true,
            Bio: "Marketing lead. Excited about positioning and narrative. When something is interesting, her question is 'how do we tell this story externally?'."),
        new("alvin",     "Alvin Tao",         "#3A7CA5", Archetype.Silent,      IsRoster: true,
            Bio: "Data analyst. Quiet, only speaks when numbers need correction or when context is missing. Always polite, never wrong about the math."),
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
