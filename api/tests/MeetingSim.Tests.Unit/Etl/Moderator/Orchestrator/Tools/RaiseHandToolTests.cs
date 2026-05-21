using MeetingSim.Etl.Moderator.Orchestrator.Tools;

namespace MeetingSim.Tests.Unit.Etl.Moderator.Orchestrator.Tools;

[TestFixture]
public class RaiseHandToolTests
{
    [Test]
    public async Task Should_post_a_hand_raised_event_for_the_named_persona()
    {
        var poster = new FakeEventPoster();
        var tool = new RaiseHandTool(ToolTestFixtures.NewRoster(), poster);

        await tool.Execute(
            ToolTestFixtures.Args("""{"persona_id":"anuj"}"""),
            ToolTestFixtures.NewContext(),
            CancellationToken.None);

        Assert.That(poster.Posted, Has.Count.EqualTo(1));
        var body = poster.Posted[0];
        Assert.Multiple(() =>
        {
            Assert.That(body["kind"]?.GetValue<string>(), Is.EqualTo("hand-raise"));
            Assert.That(body["personaId"]?.GetValue<string>(), Is.EqualTo("anuj"));
            Assert.That(body["raised"]?.GetValue<bool>(), Is.True);
        });
    }

    [Test]
    public async Task Should_post_nothing_when_persona_id_is_missing()
    {
        var poster = new FakeEventPoster();
        var tool = new RaiseHandTool(ToolTestFixtures.NewRoster(), poster);

        await tool.Execute(
            ToolTestFixtures.Args("{}"),
            ToolTestFixtures.NewContext(),
            CancellationToken.None);

        Assert.That(poster.Posted, Is.Empty);
    }

    [Test]
    public void Should_constrain_persona_id_to_non_user_roster_ids_in_its_schema()
    {
        var tool = new RaiseHandTool(ToolTestFixtures.NewRoster(), new FakeEventPoster());

        var schemaJson = tool.Definition.FunctionParameters.ToString();

        Assert.Multiple(() =>
        {
            Assert.That(schemaJson, Does.Contain("\"anuj\""));
            Assert.That(schemaJson, Does.Contain("\"serena\""));
            Assert.That(schemaJson, Does.Contain("\"kayo\""));
            Assert.That(schemaJson, Does.Not.Contain("\"you\""));
        });
    }
}
