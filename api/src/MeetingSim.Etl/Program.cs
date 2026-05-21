using MeetingSim.Etl;
using MeetingSim.Etl.Moderator;

const string SpikeCommand = "spike";
const string ModeratorCommand = "moderator";

string subcommand;
string[] subArgs;

if (args.Length > 0 && (args[0] == SpikeCommand || args[0] == ModeratorCommand))
{
    subcommand = args[0];
    subArgs = args.Skip(1).ToArray();
}
else
{
    subcommand = SpikeCommand;
    subArgs = args;
}

return subcommand switch
{
    SpikeCommand => await SpikeRunner.Run(subArgs),
    ModeratorCommand => await ModeratorRunner.Run(subArgs),
    _ => await PrintUsage(subcommand),
};

static async Task<int> PrintUsage(string unknown)
{
    await Console.Error.WriteLineAsync($"Unknown subcommand: {unknown}");
    await Console.Error.WriteLineAsync("Usage: dotnet run --project api/src/MeetingSim.Etl -- <subcommand>");
    await Console.Error.WriteLineAsync("  spike [model]                            run canned-transcript spike");
    await Console.Error.WriteLineAsync("  moderator <api-url> <session-id> [model] live moderator on a session");
    return 1;
}
