using System.Text.Json;
using MeetingSim.Etl.Moderator.Orchestrator.Interfaces;
using OpenAI.Chat;

namespace MeetingSim.Etl.Moderator.Orchestrator;

internal sealed class ModeratorToolRegistry
{
    private readonly Dictionary<string, IModeratorTool> _byName;
    private readonly IReadOnlyList<ChatTool> _definitions;

    public ModeratorToolRegistry(IEnumerable<IModeratorTool> tools)
    {
        var list = tools.ToList();
        _byName = list.ToDictionary(t => t.Name, StringComparer.Ordinal);
        _definitions = list.Select(t => t.Definition).ToList();
    }

    public IReadOnlyList<ChatTool> Definitions => _definitions;

    public IReadOnlyCollection<string> Names => _byName.Keys;

    public bool TryGet(string name, out IModeratorTool tool)
    {
        if (_byName.TryGetValue(name, out var found))
        {
            tool = found;
            return true;
        }
        tool = null!;
        return false;
    }

    public async Task Dispatch(ChatToolCall call, ModeratorContext context, CancellationToken cancellationToken)
    {
        if (!TryGet(call.FunctionName, out var tool))
        {
            await Console.Error.WriteLineAsync(
                $"[orchestrator] unknown tool '{call.FunctionName}' — dropped.").ConfigureAwait(false);
            return;
        }
        try
        {
            using var doc = JsonDocument.Parse(call.FunctionArguments);
            await tool.Execute(doc.RootElement, context, cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            await Console.Error.WriteLineAsync(
                $"[orchestrator] tool '{call.FunctionName}' got malformed JSON args: {ex.Message}")
                .ConfigureAwait(false);
        }
    }
}
