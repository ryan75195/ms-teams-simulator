using MeetingSim.Etl.Moderator;

const string ModeratorCommand = "moderator";

var moderatorArgs = args.Length > 0 && args[0] == ModeratorCommand
    ? args.Skip(1).ToArray()
    : args;

if (moderatorArgs.Length == 0)
{
    await Console.Error.WriteLineAsync(
        "Usage: dotnet run --project api/src/MeetingSim.Etl -- moderator <api-url> <session-id> [model]");
    return 1;
}

return await ModeratorRunner.Run(moderatorArgs);
