using MeetingSim.Core.Personas;
using MeetingSim.Etl.Moderator;
using OpenAI.Chat;

namespace MeetingSim.Etl;

internal static class SpikeRunner
{
    private const string DefaultModelName = "gpt-4o-mini";
    private const int RecentSpeakersWindow = 2;

    private static readonly string[] Transcript =
    [
        "Welcome everyone, today we're going through the Q2 sales report. We'll cover EMEA, APAC, and NAMER, then close with a forecast update.",
        "Our EMEA pipeline is up 18.4% quarter over quarter, with mid-market driving most of that growth at plus 24%.",
        "Average deal size has expanded to 48 thousand pounds, up 3 thousand month over month, mostly on the back of a couple of mid-market upgrades.",
        "APAC remains steady — we have two large logos at letter of intent.",
        "Anuj, you mentioned earlier you wanted to weigh in on the pricing assumptions?",
        "Any questions before we move on to NAMER?",
    ];

    public static async Task<int> Run(string[] args)
    {
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            await Console.Error.WriteLineAsync("OPENAI_API_KEY is not set. Set it in your environment and re-run.");
            return 1;
        }

        var modelName = args.Length > 0 ? args[0] : DefaultModelName;
        Console.WriteLine($"Moderator model: {modelName}");

        var roster = new PersonaRepository(new CrowdService()).Roster;
        var moderator = new ModeratorService(new ChatClient(model: modelName, apiKey: apiKey), roster);

        var seen = new List<string>();
        var recentSpeakers = new Queue<string>();
        var totalElapsedMs = 0.0;

        for (var i = 0; i < Transcript.Length; i++)
        {
            totalElapsedMs += await ProcessChunk(i, Transcript[i], moderator, seen, recentSpeakers).ConfigureAwait(false);
        }

        PrintSummary(Transcript.Length, totalElapsedMs);
        return 0;
    }

    private static async Task<double> ProcessChunk(
        int index,
        string chunk,
        ModeratorService moderator,
        List<string> seen,
        Queue<string> recentSpeakers)
    {
        Console.WriteLine();
        Console.WriteLine(new string('=', 78));
        Console.WriteLine($"[{index + 1}] Presenter: {chunk}");
        Console.WriteLine(new string('-', 78));

        var start = DateTimeOffset.UtcNow;
        var decision = await moderator.DecideAsync(chunk, seen, recentSpeakers.ToList()).ConfigureAwait(false);
        var elapsedMs = (DateTimeOffset.UtcNow - start).TotalMilliseconds;

        PrintDecision(decision, elapsedMs);
        seen.Add(chunk);
        AdvanceSpeakers(decision, recentSpeakers);
        return elapsedMs;
    }

    private static void PrintDecision(ModeratorDecision decision, double elapsedMs)
    {
        var raisedLabel = decision.Raised.HasValue ? decision.Raised.Value.ToString() : "(none)";
        Console.WriteLine($"Action     : {decision.Action}");
        Console.WriteLine($"Persona    : {decision.PersonaId ?? "(none)"}");
        Console.WriteLine($"Text       : {decision.Text ?? "(none)"}");
        Console.WriteLine($"Raised     : {raisedLabel}");
        Console.WriteLine($"Reasoning  : {decision.Reasoning}");
        Console.WriteLine($"Latency    : {elapsedMs:F0} ms");
    }

    private static void AdvanceSpeakers(ModeratorDecision decision, Queue<string> recentSpeakers)
    {
        if (string.Equals(decision.Action, "none", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (decision.PersonaId is not { Length: > 0 } speaker)
        {
            return;
        }

        recentSpeakers.Enqueue(speaker);
        while (recentSpeakers.Count > RecentSpeakersWindow)
        {
            recentSpeakers.Dequeue();
        }
    }

    private static void PrintSummary(int chunkCount, double totalElapsedMs)
    {
        Console.WriteLine();
        Console.WriteLine(new string('=', 78));
        Console.WriteLine($"Total: {chunkCount} chunks, {totalElapsedMs:F0} ms, mean {totalElapsedMs / chunkCount:F0} ms/decision");
    }
}
