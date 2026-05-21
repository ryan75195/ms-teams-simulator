using MeetingSim.Etl.Moderator.Orchestrator.Tools;

namespace MeetingSim.Tests.Unit.Etl.Moderator.Orchestrator.Tools;

[TestFixture]
public class StayQuietToolTests
{
    [Test]
    public void Should_advertise_itself_as_stay_quiet()
    {
        var tool = new StayQuietTool();

        Assert.That(tool.Name, Is.EqualTo("stay_quiet"));
    }

    [Test]
    public async Task Should_be_a_noop_when_executed()
    {
        var tool = new StayQuietTool();

        await tool.Execute(
            ToolTestFixtures.Args("""{"reason":"intro line"}"""),
            ToolTestFixtures.NewContext(),
            CancellationToken.None);

        Assert.Pass();
    }
}
