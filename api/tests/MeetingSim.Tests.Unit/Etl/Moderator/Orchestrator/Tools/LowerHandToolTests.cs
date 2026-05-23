using MeetingSim.Etl.Moderator.Orchestrator.Tools;
using static MeetingSim.Tests.Unit.Etl.Moderator.Orchestrator.Tools.ToolTestFixtures;

namespace MeetingSim.Tests.Unit.Etl.Moderator.Orchestrator.Tools;

[TestFixture]
public class LowerHandToolTests
{
    [Test]
    public async Task Should_post_a_hand_raise_event_with_raised_false()
    {
        var poster = new FakeEventPoster();
        var tool = new LowerHandTool(NewRoster(), poster);

        await tool.Execute(Args("""{"persona_id": "anuj"}"""), NewContext(), CancellationToken.None);

        Assert.That(poster.Posted, Has.Count.EqualTo(1));
        var body = poster.Posted[0];
        Assert.Multiple(() =>
        {
            Assert.That(body["kind"]!.GetValue<string>(), Is.EqualTo("hand-raise"));
            Assert.That(body["personaId"]!.GetValue<string>(), Is.EqualTo("anuj"));
            Assert.That(body["raised"]!.GetValue<bool>(), Is.False);
        });
    }

    [Test]
    public async Task Should_skip_when_persona_id_is_missing()
    {
        var poster = new FakeEventPoster();
        var tool = new LowerHandTool(NewRoster(), poster);

        await tool.Execute(Args("""{}"""), NewContext(), CancellationToken.None);

        Assert.That(poster.Posted, Is.Empty);
    }

    [Test]
    public void Should_advertise_the_lower_hand_tool_name()
    {
        var tool = new LowerHandTool(NewRoster(), new FakeEventPoster());

        Assert.That(tool.Name, Is.EqualTo("lower_hand"));
    }
}
