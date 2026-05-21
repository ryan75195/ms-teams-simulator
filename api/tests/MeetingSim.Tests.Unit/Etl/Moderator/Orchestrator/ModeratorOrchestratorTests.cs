using MeetingSim.Core.Personas;
using MeetingSim.Etl.Moderator.Orchestrator;

namespace MeetingSim.Tests.Unit.Etl.Moderator.Orchestrator;

[TestFixture]
public class ModeratorOrchestratorTests
{
    private static IReadOnlyList<Persona> Roster() => new PersonaRepository(new CrowdService()).Roster;

    private static ModeratorContext Context(
        string presenterLine = "Hello.",
        string? activeResponder = null,
        IReadOnlyCollection<string>? handsUp = null,
        IReadOnlyList<string>? recentSpeakers = null,
        IReadOnlyList<string>? recentChunks = null)
        => new(
            SessionId: Guid.NewGuid(),
            PresenterLine: presenterLine,
            RecentChunks: recentChunks ?? [],
            ActiveResponderId: activeResponder,
            HandsUp: handsUp ?? new HashSet<string>(),
            RecentSpeakers: recentSpeakers ?? [],
            Roster: Roster(),
            PersonaPreviousLines: new Dictionary<string, IReadOnlyList<string>>());

    [Test]
    public void Should_list_every_non_user_persona_in_the_system_prompt()
    {
        var roster = Roster();

        var prompt = ModeratorOrchestrator.BuildSystemPrompt(roster);

        Assert.Multiple(() =>
        {
            foreach (var persona in roster.Where(p => p.Archetype != Archetype.User))
            {
                Assert.That(prompt, Does.Contain(persona.Name));
                Assert.That(prompt, Does.Contain($"id: {persona.Id}"));
            }
        });
    }

    [Test]
    public void Should_exclude_the_user_persona_from_the_system_prompt()
    {
        var roster = Roster();
        var user = roster.Single(p => p.Archetype == Archetype.User);

        var prompt = ModeratorOrchestrator.BuildSystemPrompt(roster);

        Assert.That(prompt, Does.Not.Contain($"id: {user.Id}"));
    }

    [Test]
    public void Should_include_action_principles_in_the_system_prompt()
    {
        var prompt = ModeratorOrchestrator.BuildSystemPrompt(Roster());

        Assert.Multiple(() =>
        {
            Assert.That(prompt, Does.Contain("Speaking out loud interrupts"));
            Assert.That(prompt, Does.Contain("active responder"));
            Assert.That(prompt, Does.Contain("stay_quiet"));
        });
    }

    [Test]
    public void Should_include_the_presenter_line_in_the_user_prompt()
    {
        var prompt = ModeratorOrchestrator.BuildUserPrompt(Context(presenterLine: "EMEA pipeline is up."));

        Assert.That(prompt, Does.Contain("EMEA pipeline is up."));
        Assert.That(prompt, Does.Contain("Presenter just said:"));
    }

    [Test]
    public void Should_include_active_responder_line_when_set()
    {
        var prompt = ModeratorOrchestrator.BuildUserPrompt(Context(activeResponder: "anuj"));

        Assert.That(prompt, Does.Contain("Active responder"));
        Assert.That(prompt, Does.Contain("anuj"));
    }

    [Test]
    public void Should_omit_active_responder_line_when_unset()
    {
        var prompt = ModeratorOrchestrator.BuildUserPrompt(Context(activeResponder: null));

        Assert.That(prompt, Does.Not.Contain("Active responder"));
    }

    [Test]
    public void Should_include_hands_up_line_when_personas_have_their_hand_up()
    {
        var prompt = ModeratorOrchestrator.BuildUserPrompt(
            Context(handsUp: new HashSet<string> { "kayo", "serena" }));

        Assert.Multiple(() =>
        {
            Assert.That(prompt, Does.Contain("Hands up"));
            Assert.That(prompt, Does.Contain("kayo"));
            Assert.That(prompt, Does.Contain("serena"));
        });
    }

    [Test]
    public void Should_include_recently_spoke_line_when_speakers_present()
    {
        var prompt = ModeratorOrchestrator.BuildUserPrompt(
            Context(recentSpeakers: new[] { "anuj", "danielle" }));

        Assert.That(prompt, Does.Contain("Recently spoke"));
        Assert.That(prompt, Does.Contain("anuj, danielle"));
    }

    [Test]
    public void Should_include_recent_transcript_when_provided()
    {
        var prompt = ModeratorOrchestrator.BuildUserPrompt(
            Context(recentChunks: new[] { "Welcome.", "Pipeline up 18%." }));

        Assert.Multiple(() =>
        {
            Assert.That(prompt, Does.Contain("Recent transcript"));
            Assert.That(prompt, Does.Contain("Welcome."));
            Assert.That(prompt, Does.Contain("Pipeline up 18%."));
        });
    }

    [Test]
    public void Should_keep_only_the_last_six_transcript_chunks_as_context()
    {
        var manyChunks = new[]
        {
            "ALPHA", "BRAVO", "CHARLIE", "DELTA", "ECHO",
            "FOXTROT", "GOLF", "HOTEL", "INDIA",
        };

        var prompt = ModeratorOrchestrator.BuildUserPrompt(Context(recentChunks: manyChunks));

        Assert.Multiple(() =>
        {
            Assert.That(prompt, Does.Not.Contain("ALPHA"));
            Assert.That(prompt, Does.Not.Contain("BRAVO"));
            Assert.That(prompt, Does.Not.Contain("CHARLIE"));
            Assert.That(prompt, Does.Contain("DELTA"));
            Assert.That(prompt, Does.Contain("INDIA"));
        });
    }
}
