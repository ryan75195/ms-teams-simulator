using MeetingSim.Core.Personas;
using MeetingSim.Etl.Voice;

namespace MeetingSim.Tests.Unit.Etl.Voice;

[TestFixture]
public class OpenAIPersonaVoiceServiceTests
{
    [Test]
    public void Should_address_the_persona_by_name_in_the_system_prompt()
    {
        var persona = new Persona("anuj", "Anuj Kapoor", "#3F628E", Archetype.Skeptic, IsRoster: true);

        var systemPrompt = OpenAIPersonaVoiceService.BuildSystemPrompt(persona);

        Assert.Multiple(() =>
        {
            Assert.That(systemPrompt, Does.Contain("Anuj Kapoor"));
            Assert.That(systemPrompt, Does.Contain("push back on numbers"));
            Assert.That(systemPrompt, Does.Contain("first person"));
        });
    }

    [Test]
    public void Should_describe_each_archetype_distinctly()
    {
        var skeptic = OpenAIPersonaVoiceService.DescribeArchetype(Archetype.Skeptic);
        var curious = OpenAIPersonaVoiceService.DescribeArchetype(Archetype.Curious);
        var cheer = OpenAIPersonaVoiceService.DescribeArchetype(Archetype.Cheerleader);
        var silent = OpenAIPersonaVoiceService.DescribeArchetype(Archetype.Silent);

        Assert.Multiple(() =>
        {
            Assert.That(skeptic, Does.Not.EqualTo(curious));
            Assert.That(skeptic, Does.Not.EqualTo(cheer));
            Assert.That(skeptic, Does.Not.EqualTo(silent));
            Assert.That(curious, Does.Not.EqualTo(cheer));
            Assert.That(curious, Does.Not.EqualTo(silent));
            Assert.That(cheer, Does.Not.EqualTo(silent));
        });
    }

    [Test]
    public void Should_include_presenter_line_in_user_prompt()
    {
        var prompt = OpenAIPersonaVoiceService.BuildUserPrompt(
            "EMEA pipeline is up 18.4%",
            recentChunks: [],
            personaPreviousLines: []);

        Assert.Multiple(() =>
        {
            Assert.That(prompt, Does.Contain("EMEA pipeline is up 18.4%"));
            Assert.That(prompt, Does.Contain("The presenter just said"));
        });
    }

    [Test]
    public void Should_include_recent_chunks_section_when_chunks_provided()
    {
        var prompt = OpenAIPersonaVoiceService.BuildUserPrompt(
            "Any thoughts?",
            recentChunks: new[] { "Welcome team.", "Q3 outlook is strong." },
            personaPreviousLines: []);

        Assert.Multiple(() =>
        {
            Assert.That(prompt, Does.Contain("Welcome team."));
            Assert.That(prompt, Does.Contain("Q3 outlook is strong."));
            Assert.That(prompt, Does.Contain("What the presenter has been saying"));
        });
    }

    [Test]
    public void Should_omit_recent_chunks_section_when_no_chunks_given()
    {
        var prompt = OpenAIPersonaVoiceService.BuildUserPrompt(
            "Hello.",
            recentChunks: [],
            personaPreviousLines: []);

        Assert.That(prompt, Does.Not.Contain("What the presenter has been saying"));
    }

    [Test]
    public void Should_include_persona_previous_lines_when_provided()
    {
        var prompt = OpenAIPersonaVoiceService.BuildUserPrompt(
            "Continuing on…",
            recentChunks: [],
            personaPreviousLines: new[] { "What's the ROI on that?", "Where does the 18% come from?" });

        Assert.Multiple(() =>
        {
            Assert.That(prompt, Does.Contain("What you've already said"));
            Assert.That(prompt, Does.Contain("What's the ROI on that?"));
            Assert.That(prompt, Does.Contain("Where does the 18% come from?"));
        });
    }

    [Test]
    public void Should_omit_persona_previous_lines_section_when_empty()
    {
        var prompt = OpenAIPersonaVoiceService.BuildUserPrompt(
            "Hello.",
            recentChunks: [],
            personaPreviousLines: []);

        Assert.That(prompt, Does.Not.Contain("What you've already said"));
    }

    [Test]
    public void Should_include_slide_block_when_current_slide_provided()
    {
        var prompt = OpenAIPersonaVoiceService.BuildUserPrompt(
            "Any reactions?",
            recentChunks: [],
            personaPreviousLines: [],
            currentSlide: "Q2 Sales Report\n- EMEA pipeline +18.4% QoQ");

        Assert.Multiple(() =>
        {
            Assert.That(prompt, Does.Contain("Slide on screen"));
            Assert.That(prompt, Does.Contain("EMEA pipeline +18.4% QoQ"));
        });
    }

    [Test]
    public void Should_omit_slide_block_when_slide_empty_or_null()
    {
        var none = OpenAIPersonaVoiceService.BuildUserPrompt("Hi.", [], [], currentSlide: null);
        var blank = OpenAIPersonaVoiceService.BuildUserPrompt("Hi.", [], [], currentSlide: "   ");

        Assert.Multiple(() =>
        {
            Assert.That(none, Does.Not.Contain("Slide on screen"));
            Assert.That(blank, Does.Not.Contain("Slide on screen"));
        });
    }

    [Test]
    public void Should_keep_only_tail_of_recent_chunks_when_history_grows_large()
    {
        var chunks = new[] { "ALPHA", "BRAVO", "CHARLIE", "DELTA", "ECHO", "FOXTROT", "GOLF" };

        var prompt = OpenAIPersonaVoiceService.BuildUserPrompt(
            "Latest line.",
            recentChunks: chunks,
            personaPreviousLines: []);

        Assert.Multiple(() =>
        {
            Assert.That(prompt, Does.Not.Contain("ALPHA"));
            Assert.That(prompt, Does.Not.Contain("BRAVO"));
            Assert.That(prompt, Does.Not.Contain("CHARLIE"));
            Assert.That(prompt, Does.Contain("DELTA"));
            Assert.That(prompt, Does.Contain("ECHO"));
            Assert.That(prompt, Does.Contain("FOXTROT"));
            Assert.That(prompt, Does.Contain("GOLF"));
        });
    }
}
