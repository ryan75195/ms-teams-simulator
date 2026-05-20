using System.Text.Json;
using MeetingSim.Core.Personas;
using MeetingSim.Etl.Moderator;

namespace MeetingSim.Tests.Unit.Etl.Moderator;

[TestFixture]
public class SchemaBuilderTests
{
    private static IReadOnlyList<Persona> NewRoster() =>
        new PersonaRepository(new CrowdService()).Roster;

    [Test]
    public void Should_emit_valid_json()
    {
        var schema = SchemaBuilder.BuildModeratorDecisionSchema(NewRoster());

        Assert.DoesNotThrow(() => JsonDocument.Parse(schema));
    }

    [Test]
    public void Should_include_every_non_user_roster_id_in_the_persona_enum()
    {
        var roster = NewRoster();

        var schema = SchemaBuilder.BuildModeratorDecisionSchema(roster);

        using var doc = JsonDocument.Parse(schema);
        var personaEnum = doc.RootElement
            .GetProperty("properties")
            .GetProperty("personaId")
            .GetProperty("enum")
            .EnumerateArray()
            .Where(e => e.ValueKind == JsonValueKind.String)
            .Select(e => e.GetString())
            .ToHashSet();

        Assert.Multiple(() =>
        {
            foreach (var persona in roster.Where(p => p.Archetype != Archetype.User))
            {
                Assert.That(personaEnum, Does.Contain(persona.Id));
            }
        });
    }

    [Test]
    public void Should_exclude_the_user_persona_from_the_enum()
    {
        var roster = NewRoster();
        var user = roster.Single(p => p.Archetype == Archetype.User);

        var schema = SchemaBuilder.BuildModeratorDecisionSchema(roster);

        using var doc = JsonDocument.Parse(schema);
        var personaEnum = doc.RootElement
            .GetProperty("properties")
            .GetProperty("personaId")
            .GetProperty("enum")
            .EnumerateArray()
            .Where(e => e.ValueKind == JsonValueKind.String)
            .Select(e => e.GetString())
            .ToHashSet();

        Assert.That(personaEnum, Does.Not.Contain(user.Id));
    }

    [Test]
    public void Should_include_null_in_the_persona_enum_so_action_none_validates()
    {
        var schema = SchemaBuilder.BuildModeratorDecisionSchema(NewRoster());

        using var doc = JsonDocument.Parse(schema);
        var hasNull = doc.RootElement
            .GetProperty("properties")
            .GetProperty("personaId")
            .GetProperty("enum")
            .EnumerateArray()
            .Any(e => e.ValueKind == JsonValueKind.Null);

        Assert.That(hasNull, Is.True);
    }

    [Test]
    public void Should_constrain_action_to_the_four_supported_values()
    {
        var schema = SchemaBuilder.BuildModeratorDecisionSchema(NewRoster());

        using var doc = JsonDocument.Parse(schema);
        var actionEnum = doc.RootElement
            .GetProperty("properties")
            .GetProperty("action")
            .GetProperty("enum")
            .EnumerateArray()
            .Select(e => e.GetString())
            .ToList();

        Assert.That(actionEnum, Is.EquivalentTo(new[] { "speak", "chat", "hand-raise", "none" }));
    }
}
