using MeetingSim.Etl.Moderator.Orchestrator.Tools;
using static MeetingSim.Tests.Unit.Etl.Moderator.Orchestrator.Tools.ToolTestFixtures;

namespace MeetingSim.Tests.Unit.Etl.Moderator.Orchestrator.Tools;

[TestFixture]
public class SetActiveResponderToolTests
{
    [Test]
    public async Task Should_set_the_active_responder_when_persona_id_is_provided()
    {
        var state = new FakeModeratorStateMutator();
        var tool = new SetActiveResponderTool(NewRoster(), state);

        await tool.Execute(Args("""{"persona_id": "serena"}"""), NewContext(), CancellationToken.None);

        Assert.That(state.Current, Is.EqualTo("serena"));
    }

    [Test]
    public async Task Should_clear_the_active_responder_when_persona_id_is_missing()
    {
        var state = new FakeModeratorStateMutator();
        var tool = new SetActiveResponderTool(NewRoster(), state);

        await tool.Execute(Args("""{}"""), NewContext(), CancellationToken.None);

        Assert.That(state.Current, Is.Null);
    }

    [Test]
    public async Task Should_clear_the_active_responder_when_persona_id_is_empty_string()
    {
        var state = new FakeModeratorStateMutator();
        state.SetActiveResponder("anuj");
        var tool = new SetActiveResponderTool(NewRoster(), state);

        await tool.Execute(Args("""{"persona_id": ""}"""), NewContext(), CancellationToken.None);

        Assert.That(state.Current, Is.Null);
    }

    [Test]
    public void Should_advertise_the_set_active_responder_tool_name()
    {
        var tool = new SetActiveResponderTool(NewRoster(), new FakeModeratorStateMutator());

        Assert.That(tool.Name, Is.EqualTo("set_active_responder"));
    }
}
