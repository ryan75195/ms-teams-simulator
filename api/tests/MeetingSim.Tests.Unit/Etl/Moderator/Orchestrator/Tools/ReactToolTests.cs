using MeetingSim.Etl.Moderator.Orchestrator.Tools;

namespace MeetingSim.Tests.Unit.Etl.Moderator.Orchestrator.Tools;

[TestFixture]
public class ReactToolTests
{
    [Test]
    public async Task Should_post_reaction_with_tile_derived_from_persona_index()
    {
        var poster = new FakeEventPoster();
        var tool = new ReactTool(ToolTestFixtures.NewRoster(), poster);

        await tool.Execute(
            ToolTestFixtures.Args("""{"persona_id":"kayo","emoji":"🎉"}"""),
            ToolTestFixtures.NewContext(),
            CancellationToken.None);

        Assert.That(poster.Posted, Has.Count.EqualTo(1));
        var body = poster.Posted[0];
        Assert.Multiple(() =>
        {
            Assert.That(body["kind"]?.GetValue<string>(), Is.EqualTo("reaction"));
            Assert.That(body["tile"]?.GetValue<int>(), Is.EqualTo(3));
            Assert.That(body["emoji"]?.GetValue<string>(), Is.EqualTo("🎉"));
        });
    }

    [Test]
    public async Task Should_skip_when_persona_id_is_missing()
    {
        var poster = new FakeEventPoster();
        var tool = new ReactTool(ToolTestFixtures.NewRoster(), poster);

        await tool.Execute(
            ToolTestFixtures.Args("""{"emoji":"👍"}"""),
            ToolTestFixtures.NewContext(),
            CancellationToken.None);

        Assert.That(poster.Posted, Is.Empty);
    }

    [Test]
    public async Task Should_skip_when_emoji_is_missing()
    {
        var poster = new FakeEventPoster();
        var tool = new ReactTool(ToolTestFixtures.NewRoster(), poster);

        await tool.Execute(
            ToolTestFixtures.Args("""{"persona_id":"anuj"}"""),
            ToolTestFixtures.NewContext(),
            CancellationToken.None);

        Assert.That(poster.Posted, Is.Empty);
    }

    [Test]
    public void Should_constrain_emoji_choices_in_its_schema()
    {
        var tool = new ReactTool(ToolTestFixtures.NewRoster(), new FakeEventPoster());

        var schemaJson = tool.Definition.FunctionParameters.ToString();

        Assert.Multiple(() =>
        {
            Assert.That(schemaJson, Does.Contain("👍"));
            Assert.That(schemaJson, Does.Contain("🎉"));
            Assert.That(schemaJson, Does.Contain("😮"));
        });
    }
}
