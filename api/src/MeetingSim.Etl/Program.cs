using MeetingSim.Core.Personas;
using MeetingSim.Etl.Moderator;
using OpenAI.Chat;

const string ModelName = "gpt-4o-mini";

var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
if (string.IsNullOrWhiteSpace(apiKey))
{
    await Console.Error.WriteLineAsync("OPENAI_API_KEY is not set. Set it in your environment and re-run.");
    return 1;
}

var roster = new PersonaRepository(new CrowdService()).Roster;

var transcript = new[]
{
    "Welcome everyone, today we're going through the Q2 sales report. We'll cover EMEA, APAC, and NAMER, then close with a forecast update.",
    "Our EMEA pipeline is up 18.4% quarter over quarter, with mid-market driving most of that growth at plus 24%.",
    "Average deal size has expanded to 48 thousand pounds, up 3 thousand month over month, mostly on the back of a couple of mid-market upgrades.",
    "APAC remains steady — we have two large logos at letter of intent.",
    "Anuj, you mentioned earlier you wanted to weigh in on the pricing assumptions?",
    "Any questions before we move on to NAMER?",
};

var client = new ChatClient(model: ModelName, apiKey: apiKey);
var moderator = new ModeratorService(client, roster);

var seen = new List<string>();
var totalElapsedMs = 0.0;

for (var i = 0; i < transcript.Length; i++)
{
    var chunk = transcript[i];
    Console.WriteLine();
    Console.WriteLine(new string('=', 78));
    Console.WriteLine($"[{i + 1}] Presenter: {chunk}");
    Console.WriteLine(new string('-', 78));

    var start = DateTimeOffset.UtcNow;
    var decision = await moderator.DecideAsync(chunk, seen);
    var elapsedMs = (DateTimeOffset.UtcNow - start).TotalMilliseconds;
    totalElapsedMs += elapsedMs;

    var raisedLabel = decision.Raised.HasValue ? decision.Raised.Value.ToString() : "(none)";
    Console.WriteLine($"Action     : {decision.Action}");
    Console.WriteLine($"Persona    : {decision.PersonaId ?? "(none)"}");
    Console.WriteLine($"Text       : {decision.Text ?? "(none)"}");
    Console.WriteLine($"Raised     : {raisedLabel}");
    Console.WriteLine($"Reasoning  : {decision.Reasoning}");
    Console.WriteLine($"Latency    : {elapsedMs:F0} ms");

    seen.Add(chunk);
}

Console.WriteLine();
Console.WriteLine(new string('=', 78));
Console.WriteLine($"Total: {transcript.Length} chunks, {totalElapsedMs:F0} ms, mean {totalElapsedMs / transcript.Length:F0} ms/decision");

return 0;
