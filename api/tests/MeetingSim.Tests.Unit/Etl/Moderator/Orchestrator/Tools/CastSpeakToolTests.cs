using MeetingSim.Etl.Moderator.Orchestrator.Tools;

namespace MeetingSim.Tests.Unit.Etl.Moderator.Orchestrator.Tools;

[TestFixture]
public class CastSpeakToolTests
{
    [Test]
    public async Task Should_post_speak_event_with_voice_generated_text()
    {
        var poster = new FakeEventPoster();
        var voice = new FakePersonaVoiceService { CannedResponse = "What's the ROI on that?" };
        var tool = new CastSpeakTool(ToolTestFixtures.NewRoster(), poster, voice);

        await tool.Execute(
            ToolTestFixtures.Args("""{"persona_id":"anuj"}"""),
            ToolTestFixtures.NewContext(presenterLine: "Pipeline is up 18%"),
            CancellationToken.None);

        Assert.That(poster.Posted, Has.Count.EqualTo(1));
        var body = poster.Posted[0];
        Assert.Multiple(() =>
        {
            Assert.That(body["kind"]?.GetValue<string>(), Is.EqualTo("speak"));
            Assert.That(body["personaId"]?.GetValue<string>(), Is.EqualTo("anuj"));
            Assert.That(body["text"]?.GetValue<string>(), Is.EqualTo("What's the ROI on that?"));
            Assert.That(body["durationMs"]?.GetValue<int>(), Is.EqualTo(3000));
            Assert.That(voice.RequestedPersonas, Is.EqualTo(new[] { "anuj" }));
        });
    }

    [Test]
    public async Task Should_also_post_hand_lowered_when_persona_had_hand_up()
    {
        var poster = new FakeEventPoster();
        var voice = new FakePersonaVoiceService { CannedResponse = "Yes, thanks for calling on me." };
        var tool = new CastSpeakTool(ToolTestFixtures.NewRoster(), poster, voice);

        var context = ToolTestFixtures.NewContext(handsUp: new HashSet<string> { "anuj" });

        await tool.Execute(
            ToolTestFixtures.Args("""{"persona_id":"anuj"}"""),
            context,
            CancellationToken.None);

        Assert.That(poster.Posted, Has.Count.EqualTo(2));
        Assert.Multiple(() =>
        {
            Assert.That(poster.Posted[0]["kind"]?.GetValue<string>(), Is.EqualTo("speak"));
            Assert.That(poster.Posted[1]["kind"]?.GetValue<string>(), Is.EqualTo("hand-raise"));
            Assert.That(poster.Posted[1]["raised"]?.GetValue<bool>(), Is.False);
            Assert.That(poster.Posted[1]["personaId"]?.GetValue<string>(), Is.EqualTo("anuj"));
        });
    }

    [Test]
    public async Task Should_not_post_hand_lowered_when_persona_did_not_have_hand_up()
    {
        var poster = new FakeEventPoster();
        var voice = new FakePersonaVoiceService { CannedResponse = "OK" };
        var tool = new CastSpeakTool(ToolTestFixtures.NewRoster(), poster, voice);

        await tool.Execute(
            ToolTestFixtures.Args("""{"persona_id":"serena"}"""),
            ToolTestFixtures.NewContext(handsUp: new HashSet<string> { "anuj" }),
            CancellationToken.None);

        Assert.That(poster.Posted, Has.Count.EqualTo(1));
        Assert.That(poster.Posted[0]["kind"]?.GetValue<string>(), Is.EqualTo("speak"));
    }

    [Test]
    public async Task Should_skip_when_voice_service_returns_empty()
    {
        var poster = new FakeEventPoster();
        var voice = new FakePersonaVoiceService { CannedResponse = "" };
        var tool = new CastSpeakTool(ToolTestFixtures.NewRoster(), poster, voice);

        await tool.Execute(
            ToolTestFixtures.Args("""{"persona_id":"anuj"}"""),
            ToolTestFixtures.NewContext(),
            CancellationToken.None);

        Assert.That(poster.Posted, Is.Empty);
    }

    [Test]
    public async Task Should_pass_persona_previous_lines_to_voice_service_when_available()
    {
        var poster = new FakeEventPoster();
        var voice = new FakePersonaVoiceService { CannedResponse = "Following up on my earlier point…" };
        var tool = new CastSpeakTool(ToolTestFixtures.NewRoster(), poster, voice);

        var previousLines = new Dictionary<string, IReadOnlyList<string>>
        {
            ["anuj"] = new[] { "What about ROI?" },
        };

        await tool.Execute(
            ToolTestFixtures.Args("""{"persona_id":"anuj"}"""),
            ToolTestFixtures.NewContext(personaPreviousLines: previousLines),
            CancellationToken.None);

        Assert.That(voice.RequestedPersonas, Is.EqualTo(new[] { "anuj" }));
        Assert.That(poster.Posted, Has.Count.EqualTo(1));
    }
}
