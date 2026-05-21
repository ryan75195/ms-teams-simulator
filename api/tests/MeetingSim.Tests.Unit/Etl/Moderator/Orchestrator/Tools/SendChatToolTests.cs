using MeetingSim.Etl.Moderator.Orchestrator.Tools;

namespace MeetingSim.Tests.Unit.Etl.Moderator.Orchestrator.Tools;

[TestFixture]
public class SendChatToolTests
{
    [Test]
    public async Task Should_post_chat_with_supplied_text_when_provided()
    {
        var poster = new FakeEventPoster();
        var voice = new FakePersonaVoiceService { CannedResponse = "voice fallback" };
        var tool = new SendChatTool(ToolTestFixtures.NewRoster(), poster, voice);

        await tool.Execute(
            ToolTestFixtures.Args("""{"persona_id":"serena","text":"+1"}"""),
            ToolTestFixtures.NewContext(),
            CancellationToken.None);

        Assert.That(poster.Posted, Has.Count.EqualTo(1));
        var body = poster.Posted[0];
        Assert.Multiple(() =>
        {
            Assert.That(body["kind"]?.GetValue<string>(), Is.EqualTo("chat"));
            Assert.That(body["personaId"]?.GetValue<string>(), Is.EqualTo("serena"));
            Assert.That(body["text"]?.GetValue<string>(), Is.EqualTo("+1"));
            Assert.That(voice.RequestedPersonas, Is.Empty);
        });
    }

    [Test]
    public async Task Should_fall_back_to_voice_service_when_text_omitted()
    {
        var poster = new FakeEventPoster();
        var voice = new FakePersonaVoiceService { CannedResponse = "Got it" };
        var tool = new SendChatTool(ToolTestFixtures.NewRoster(), poster, voice);

        await tool.Execute(
            ToolTestFixtures.Args("""{"persona_id":"anuj"}"""),
            ToolTestFixtures.NewContext(),
            CancellationToken.None);

        Assert.That(poster.Posted, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(poster.Posted[0]["text"]?.GetValue<string>(), Is.EqualTo("Got it"));
            Assert.That(voice.RequestedPersonas, Is.EqualTo(new[] { "anuj" }));
        });
    }

    [Test]
    public async Task Should_skip_when_voice_service_returns_empty_text()
    {
        var poster = new FakeEventPoster();
        var voice = new FakePersonaVoiceService { CannedResponse = "   " };
        var tool = new SendChatTool(ToolTestFixtures.NewRoster(), poster, voice);

        await tool.Execute(
            ToolTestFixtures.Args("""{"persona_id":"anuj"}"""),
            ToolTestFixtures.NewContext(),
            CancellationToken.None);

        Assert.That(poster.Posted, Is.Empty);
    }
}
