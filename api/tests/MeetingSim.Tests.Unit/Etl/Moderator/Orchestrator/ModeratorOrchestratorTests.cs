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
        IReadOnlyList<string>? recentChunks = null,
        string? currentSlide = null)
        => new(
            SessionId: Guid.NewGuid(),
            PresenterLine: presenterLine,
            RecentChunks: recentChunks ?? [],
            ActiveResponderId: activeResponder,
            HandsUp: handsUp ?? new HashSet<string>(),
            RecentSpeakers: recentSpeakers ?? [],
            Roster: Roster(),
            PersonaPreviousLines: new Dictionary<string, IReadOnlyList<string>>(),
            CurrentSlide: currentSlide);

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
            Assert.That(prompt, Does.Contain("Address routing"));
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
    public void Should_instruct_the_model_to_infer_addressed_personas_with_stt_variance()
    {
        var prompt = ModeratorOrchestrator.BuildSystemPrompt(Roster());

        Assert.Multiple(() =>
        {
            Assert.That(prompt, Does.Contain("Address routing"));
            Assert.That(prompt, Does.Contain("speech-to-text variance"));
        });
    }

    [Test]
    public void Should_repeat_the_active_responder_must_rule_near_the_end_of_the_system_prompt()
    {
        var prompt = ModeratorOrchestrator.BuildSystemPrompt(Roster());
        var firstIndex = prompt.IndexOf("active responder", StringComparison.Ordinal);
        var lastIndex = prompt.LastIndexOf("active responder", StringComparison.Ordinal);

        Assert.That(lastIndex, Is.GreaterThan(firstIndex), "active responder rule should appear at top and bottom of the prompt to mitigate context rot");
        Assert.That(prompt[lastIndex..], Does.Contain("cast_speak"));
    }

    [Test]
    public void Should_include_recent_decisions_in_the_user_prompt_when_provided()
    {
        var prompt = ModeratorOrchestrator.BuildUserPrompt(
            ContextWithRecentDecisions(new[] { "complete: cast_speak(anuj)", "silence: send_chat(serena)" }));

        Assert.Multiple(() =>
        {
            Assert.That(prompt, Does.Contain("Your recent decisions"));
            Assert.That(prompt, Does.Contain("cast_speak(anuj)"));
            Assert.That(prompt, Does.Contain("send_chat(serena)"));
        });
    }

    [Test]
    public void Should_omit_recent_decisions_section_when_empty()
    {
        var prompt = ModeratorOrchestrator.BuildUserPrompt(Context());

        Assert.That(prompt, Does.Not.Contain("Your recent decisions"));
    }

    private static ModeratorContext ContextWithRecentDecisions(IReadOnlyList<string> recent) => new(
        SessionId: Guid.NewGuid(),
        PresenterLine: "Hello.",
        RecentChunks: [],
        ActiveResponderId: null,
        HandsUp: new HashSet<string>(),
        RecentSpeakers: [],
        Roster: Roster(),
        PersonaPreviousLines: new Dictionary<string, IReadOnlyList<string>>(),
        CurrentSlide: null,
        Mode: "complete",
        RecentDecisions: recent);

    [Test]
    public void Should_describe_set_active_responder_and_lower_hand_in_the_system_prompt()
    {
        var prompt = ModeratorOrchestrator.BuildSystemPrompt(Roster());

        Assert.Multiple(() =>
        {
            Assert.That(prompt, Does.Contain("set_active_responder"));
            Assert.That(prompt, Does.Contain("lower_hand"));
        });
    }

    [Test]
    public void Should_include_slide_block_when_current_slide_set()
    {
        var prompt = ModeratorOrchestrator.BuildUserPrompt(
            Context(currentSlide: "Q2 Sales Report\n- EMEA pipeline +18.4% QoQ"));

        Assert.Multiple(() =>
        {
            Assert.That(prompt, Does.Contain("Slide on screen"));
            Assert.That(prompt, Does.Contain("EMEA pipeline +18.4% QoQ"));
        });
    }

    [Test]
    public void Should_omit_slide_block_when_slide_empty_or_null()
    {
        var none = ModeratorOrchestrator.BuildUserPrompt(Context(currentSlide: null));
        var blank = ModeratorOrchestrator.BuildUserPrompt(Context(currentSlide: "   "));

        Assert.Multiple(() =>
        {
            Assert.That(none, Does.Not.Contain("Slide on screen"));
            Assert.That(blank, Does.Not.Contain("Slide on screen"));
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
