using MeetingSim.Core.Personas;
using MeetingSim.Etl.Moderator;

namespace MeetingSim.Tests.Unit.Etl.Moderator;

[TestFixture]
public class PromptBuilderTests
{
    private static readonly IReadOnlyList<string> NoRecentSpeakers = [];
    private static readonly IReadOnlyList<string> NoRecentChunks = [];

    private static IReadOnlyList<Persona> NewRoster() =>
        new PersonaRepository(new CrowdService()).Roster;

    [Test]
    public void Should_list_every_non_user_roster_persona_in_the_system_prompt()
    {
        var roster = NewRoster();

        var systemPrompt = PromptBuilder.BuildSystemPrompt(roster);

        Assert.Multiple(() =>
        {
            foreach (var persona in roster.Where(p => p.Archetype != Archetype.User))
            {
                Assert.That(systemPrompt, Does.Contain(persona.Name));
                Assert.That(systemPrompt, Does.Contain($"id: {persona.Id}"));
            }
        });
    }

    [Test]
    public void Should_exclude_the_user_persona_from_the_system_prompt()
    {
        var roster = NewRoster();
        var user = roster.Single(p => p.Archetype == Archetype.User);

        var systemPrompt = PromptBuilder.BuildSystemPrompt(roster);

        Assert.That(systemPrompt, Does.Not.Contain($"id: {user.Id}"));
    }

    [Test]
    public void Should_describe_each_archetype_used_in_the_roster()
    {
        var roster = NewRoster();

        var systemPrompt = PromptBuilder.BuildSystemPrompt(roster);

        Assert.Multiple(() =>
        {
            Assert.That(systemPrompt, Does.Contain("skeptic"));
            Assert.That(systemPrompt, Does.Contain("curious"));
            Assert.That(systemPrompt, Does.Contain("cheerleader"));
            Assert.That(systemPrompt, Does.Contain("silent"));
        });
    }

    [Test]
    public void Should_include_the_current_chunk_in_the_user_prompt()
    {
        var prompt = PromptBuilder.BuildUserPrompt("EMEA is up 18%", NoRecentChunks, NoRecentSpeakers);

        Assert.That(prompt, Does.Contain("EMEA is up 18%"));
        Assert.That(prompt, Does.Contain("Presenter just said:"));
    }

    [Test]
    public void Should_include_recent_chunks_as_context_in_the_user_prompt()
    {
        var prompt = PromptBuilder.BuildUserPrompt(
            "Any questions?",
            new[] { "Welcome.", "Pipeline up 18%." },
            NoRecentSpeakers);

        Assert.Multiple(() =>
        {
            Assert.That(prompt, Does.Contain("Welcome."));
            Assert.That(prompt, Does.Contain("Pipeline up 18%."));
            Assert.That(prompt, Does.Contain("Recent transcript so far:"));
        });
    }

    [Test]
    public void Should_skip_the_context_section_when_no_recent_chunks()
    {
        var prompt = PromptBuilder.BuildUserPrompt("Welcome.", NoRecentChunks, NoRecentSpeakers);

        Assert.That(prompt, Does.Not.Contain("Recent transcript so far:"));
    }

    [Test]
    public void Should_include_recent_speakers_with_steering_in_the_user_prompt()
    {
        var prompt = PromptBuilder.BuildUserPrompt(
            "Now on to NAMER.",
            NoRecentChunks,
            new[] { "anuj", "serena" });

        Assert.Multiple(() =>
        {
            Assert.That(prompt, Does.Contain("Recently spoke: anuj, serena"));
            Assert.That(prompt, Does.Contain("Pick someone else"));
            Assert.That(prompt, Does.Contain("unless the presenter directly addresses one of them"));
        });
    }

    [Test]
    public void Should_skip_the_recent_speakers_section_when_no_speakers_given()
    {
        var prompt = PromptBuilder.BuildUserPrompt("Hello.", NoRecentChunks, NoRecentSpeakers);

        Assert.That(prompt, Does.Not.Contain("Recently spoke:"));
        Assert.That(prompt, Does.Not.Contain("Pick someone else"));
    }

    [Test]
    public void Should_emit_direct_callout_block_when_persona_is_named()
    {
        var prompt = PromptBuilder.BuildUserPrompt(
            "Anuj, what do you think?",
            NoRecentChunks,
            NoRecentSpeakers,
            calledOutPersonaId: "anuj");

        Assert.Multiple(() =>
        {
            Assert.That(prompt, Does.Contain("DIRECT CALLOUT"));
            Assert.That(prompt, Does.Contain("'anuj'"));
            Assert.That(prompt, Does.Contain("MUST respond with action='speak'"));
        });
    }

    [Test]
    public void Should_skip_direct_callout_block_when_no_persona_named()
    {
        var prompt = PromptBuilder.BuildUserPrompt(
            "Pipeline is up.",
            NoRecentChunks,
            NoRecentSpeakers,
            calledOutPersonaId: null);

        Assert.That(prompt, Does.Not.Contain("DIRECT CALLOUT"));
    }
}
