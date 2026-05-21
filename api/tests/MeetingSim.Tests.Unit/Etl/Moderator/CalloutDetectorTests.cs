using MeetingSim.Core.Personas;
using MeetingSim.Etl.Moderator;

namespace MeetingSim.Tests.Unit.Etl.Moderator;

[TestFixture]
public class CalloutDetectorTests
{
    private static IReadOnlyList<Persona> NewRoster() =>
        new PersonaRepository(new CrowdService()).Roster;

    [Test]
    public void Should_match_a_persona_first_name_by_word_boundary()
    {
        var roster = NewRoster();

        var match = CalloutDetector.Detect("Anuj, what do you think about that?", roster);

        Assert.That(match, Is.EqualTo("anuj"));
    }

    [Test]
    public void Should_match_case_insensitively()
    {
        var roster = NewRoster();

        var match = CalloutDetector.Detect("serena, can you weigh in?", roster);

        Assert.That(match, Is.EqualTo("serena"));
    }

    [Test]
    public void Should_match_first_name_only_for_compound_last_names()
    {
        var roster = NewRoster();

        var match = CalloutDetector.Detect("Charlotte, your thoughts?", roster);

        Assert.That(match, Is.EqualTo("charlotte"));
    }

    [Test]
    public void Should_return_null_when_no_persona_name_is_present()
    {
        var roster = NewRoster();

        var match = CalloutDetector.Detect("Our pipeline is up eighteen percent this quarter.", roster);

        Assert.That(match, Is.Null);
    }

    [Test]
    public void Should_not_match_when_name_is_a_substring_of_another_word()
    {
        var roster = NewRoster();

        var match = CalloutDetector.Detect("The anujection of capital was approved.", roster);

        Assert.That(match, Is.Null);
    }

    [Test]
    public void Should_skip_the_user_persona()
    {
        var roster = NewRoster();
        var user = roster.Single(p => p.Archetype == Archetype.User);
        var firstName = user.Name.Split(' ')[0];

        var match = CalloutDetector.Detect($"Hey {firstName}, you're up.", roster);

        Assert.That(match, Is.Null.Or.Not.EqualTo(user.Id));
    }

    [Test]
    public void Should_return_null_for_blank_input()
    {
        var roster = NewRoster();

        Assert.Multiple(() =>
        {
            Assert.That(CalloutDetector.Detect("", roster), Is.Null);
            Assert.That(CalloutDetector.Detect("   ", roster), Is.Null);
        });
    }
}
