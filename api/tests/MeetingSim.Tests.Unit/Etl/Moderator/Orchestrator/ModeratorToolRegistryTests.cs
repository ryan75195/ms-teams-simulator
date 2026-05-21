using System.Text.Json;
using MeetingSim.Etl.Moderator.Orchestrator;
using MeetingSim.Etl.Moderator.Orchestrator.Interfaces;
using OpenAI.Chat;

namespace MeetingSim.Tests.Unit.Etl.Moderator.Orchestrator;

[TestFixture]
public class ModeratorToolRegistryTests
{
    private static ModeratorContext NewContext() => new(
        SessionId: Guid.NewGuid(),
        PresenterLine: "Hello.",
        RecentChunks: [],
        ActiveResponderId: null,
        HandsUp: new HashSet<string>(),
        RecentSpeakers: [],
        Roster: [],
        PersonaPreviousLines: new Dictionary<string, IReadOnlyList<string>>());

    [Test]
    public void Should_expose_each_registered_tool_definition()
    {
        var alpha = new FakeTool("alpha");
        var bravo = new FakeTool("bravo");

        var registry = new ModeratorToolRegistry([alpha, bravo]);

        Assert.That(registry.Definitions, Has.Count.EqualTo(2));
    }

    [Test]
    public void Should_find_a_tool_by_name()
    {
        var alpha = new FakeTool("alpha");
        var registry = new ModeratorToolRegistry([alpha]);

        var found = registry.TryGet("alpha", out var resolved);

        Assert.Multiple(() =>
        {
            Assert.That(found, Is.True);
            Assert.That(resolved, Is.SameAs(alpha));
        });
    }

    [Test]
    public void Should_return_false_for_unknown_tool_name()
    {
        var registry = new ModeratorToolRegistry([new FakeTool("alpha")]);

        var found = registry.TryGet("nonesuch", out _);

        Assert.That(found, Is.False);
    }

    [Test]
    public async Task Should_dispatch_to_the_matching_tool_with_parsed_arguments()
    {
        var alpha = new FakeTool("alpha");
        var registry = new ModeratorToolRegistry([alpha]);
        var call = MakeToolCall("alpha", """{"persona_id":"anuj"}""");

        await registry.Dispatch(call, NewContext(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(alpha.Invocations, Has.Count.EqualTo(1));
            Assert.That(alpha.Invocations[0].GetProperty("persona_id").GetString(), Is.EqualTo("anuj"));
        });
    }

    [Test]
    public async Task Should_drop_calls_to_unknown_tools_without_throwing()
    {
        var alpha = new FakeTool("alpha");
        var registry = new ModeratorToolRegistry([alpha]);
        var call = MakeToolCall("ghost", "{}");

        await registry.Dispatch(call, NewContext(), CancellationToken.None);

        Assert.That(alpha.Invocations, Is.Empty);
    }

    [Test]
    public async Task Should_swallow_json_parse_errors_in_tool_arguments()
    {
        var alpha = new FakeTool("alpha");
        var registry = new ModeratorToolRegistry([alpha]);
        var call = MakeToolCall("alpha", "{not json");

        await registry.Dispatch(call, NewContext(), CancellationToken.None);

        Assert.That(alpha.Invocations, Is.Empty);
    }

    private static ChatToolCall MakeToolCall(string name, string argsJson)
        => ChatToolCall.CreateFunctionToolCall(
            id: "call_" + Guid.NewGuid().ToString("N"),
            functionName: name,
            functionArguments: BinaryData.FromString(argsJson));

    private sealed class FakeTool : IModeratorTool
    {
        private readonly ChatTool _definition;

        public FakeTool(string name)
        {
            Name = name;
            _definition = ChatTool.CreateFunctionTool(
                functionName: name,
                functionDescription: "test fixture tool",
                functionParameters: BinaryData.FromString("""{"type":"object"}"""));
        }

        public List<JsonElement> Invocations { get; } = [];

        public string Name { get; }

        public ChatTool Definition => _definition;

        public Task Execute(JsonElement arguments, ModeratorContext context, CancellationToken cancellationToken)
        {
            Invocations.Add(arguments.Clone());
            return Task.CompletedTask;
        }
    }
}
