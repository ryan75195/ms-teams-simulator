using MeetingSim.Core.Personas;

namespace MeetingSim.Tests.Unit.Core.Personas;

[TestFixture]
public class CrowdServiceTests
{
    [Test]
    public void Should_generate_the_same_persona_for_the_same_seed_and_index()
    {
        var generator = new CrowdService();

        var first = generator.Generate(seed: 4815, index: 12);
        var second = generator.Generate(seed: 4815, index: 12);

        Assert.That(first, Is.EqualTo(second));
    }

    [Test]
    public void Should_generate_different_personas_for_different_indexes()
    {
        var generator = new CrowdService();

        var a = generator.Generate(seed: 99, index: 0);
        var b = generator.Generate(seed: 99, index: 1);

        Assert.That(a.Id, Is.Not.EqualTo(b.Id));
    }

    [Test]
    public void Should_format_the_crowd_id_as_crowd_seed_index()
    {
        var generator = new CrowdService();

        var persona = generator.Generate(seed: 4815, index: 12);

        Assert.That(persona.Id, Is.EqualTo("crowd:4815:12"));
    }

    [Test]
    public void Should_mark_crowd_personas_as_not_roster_with_unknown_archetype()
    {
        var generator = new CrowdService();

        var persona = generator.Generate(seed: 1, index: 1);

        Assert.Multiple(() =>
        {
            Assert.That(persona.IsRoster, Is.False);
            Assert.That(persona.Archetype, Is.EqualTo(Archetype.Unknown));
            Assert.That(persona.Name, Is.Not.Empty);
            Assert.That(persona.Color, Does.StartWith("#"));
        });
    }

    [Test]
    public void Should_try_parse_a_well_formed_crowd_id()
    {
        var generator = new CrowdService();

        var parsed = generator.TryParseCrowdId("crowd:4815:12", out var seed, out var index);

        Assert.Multiple(() =>
        {
            Assert.That(parsed, Is.True);
            Assert.That(seed, Is.EqualTo(4815));
            Assert.That(index, Is.EqualTo(12));
        });
    }

    [Test]
    public void Should_try_parse_return_false_for_roster_ids()
    {
        var generator = new CrowdService();

        var parsed = generator.TryParseCrowdId("anuj", out _, out _);

        Assert.That(parsed, Is.False);
    }

    [Test]
    public void Should_try_parse_return_false_for_malformed_crowd_ids()
    {
        var generator = new CrowdService();

        var malformedSegments = generator.TryParseCrowdId("crowd:4815", out _, out _);
        var malformedNumbers = generator.TryParseCrowdId("crowd:nope:nada", out _, out _);

        Assert.Multiple(() =>
        {
            Assert.That(malformedSegments, Is.False);
            Assert.That(malformedNumbers, Is.False);
        });
    }
}
