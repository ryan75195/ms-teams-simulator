using MeetingSim.Core.Personas;

namespace MeetingSim.Tests.Unit.Core.Personas;

[TestFixture]
public class PersonaRepositoryTests
{
    private static PersonaRepository NewCatalog() => new(new CrowdService());

    [Test]
    public void Should_expose_a_fixed_roster_with_user_first()
    {
        var catalog = NewCatalog();

        Assert.Multiple(() =>
        {
            Assert.That(catalog.Roster, Is.Not.Empty);
            Assert.That(catalog.Roster[0].Id, Is.EqualTo("you"));
            Assert.That(catalog.Roster[0].Archetype, Is.EqualTo(Archetype.User));
            Assert.That(catalog.Roster.All(p => p.IsRoster), Is.True);
        });
    }

    [Test]
    public void Should_resolve_a_roster_persona_by_id()
    {
        var catalog = NewCatalog();

        var resolved = catalog.Resolve("anuj");

        Assert.That(resolved, Is.Not.Null);
        Assert.That(resolved!.Name, Is.EqualTo("Anuj Kapoor"));
    }

    [Test]
    public void Should_resolve_a_crowd_persona_by_id()
    {
        var catalog = NewCatalog();

        var resolved = catalog.Resolve("crowd:4815:7");

        Assert.That(resolved, Is.Not.Null);
        Assert.That(resolved!.Id, Is.EqualTo("crowd:4815:7"));
        Assert.That(resolved.IsRoster, Is.False);
    }

    [Test]
    public void Should_return_null_when_resolving_unknown_id()
    {
        var catalog = NewCatalog();

        var resolved = catalog.Resolve("nope");

        Assert.That(resolved, Is.Null);
    }

    [Test]
    public void Should_return_null_when_resolving_empty_id()
    {
        var catalog = NewCatalog();

        var resolved = catalog.Resolve(string.Empty);

        Assert.That(resolved, Is.Null);
    }

    [Test]
    public void Should_paginate_the_crowd_by_skip_and_count()
    {
        var catalog = NewCatalog();

        var firstPage = catalog.Crowd(seed: 4815, skip: 0, count: 3);
        var secondPage = catalog.Crowd(seed: 4815, skip: 3, count: 3);

        Assert.Multiple(() =>
        {
            Assert.That(firstPage, Has.Count.EqualTo(3));
            Assert.That(secondPage, Has.Count.EqualTo(3));
            Assert.That(firstPage[0].Id, Is.EqualTo("crowd:4815:0"));
            Assert.That(secondPage[0].Id, Is.EqualTo("crowd:4815:3"));
            Assert.That(firstPage.Select(p => p.Id).Intersect(secondPage.Select(p => p.Id)), Is.Empty);
        });
    }

    [Test]
    public void Should_return_empty_crowd_for_non_positive_count()
    {
        var catalog = NewCatalog();

        var result = catalog.Crowd(seed: 1, skip: 0, count: 0);

        Assert.That(result, Is.Empty);
    }

    [Test]
    public void Should_produce_a_stable_crowd_for_the_same_seed()
    {
        var catalog = NewCatalog();

        var first = catalog.Crowd(seed: 99, skip: 0, count: 5);
        var second = catalog.Crowd(seed: 99, skip: 0, count: 5);

        Assert.That(first, Is.EqualTo(second));
    }
}
