using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace MeetingSim.Etl.Moderator;

internal sealed class ApiEventClient
{
    private readonly HttpClient _http;
    private readonly Guid _sessionId;

    public ApiEventClient(HttpClient http, Guid sessionId)
    {
        _http = http;
        _sessionId = sessionId;
    }

    public async Task Post(ModeratorDecision decision, CancellationToken cancellationToken = default)
    {
        var body = EventPostBodyFactory.FromDecision(decision);
        if (body is null)
        {
            await Console.Error.WriteLineAsync(
                $"[skipped] decision '{decision.Action}' had no postable body — likely missing personaId or text.")
                .ConfigureAwait(false);
            return;
        }

        var response = await _http
            .PostAsJsonAsync<JsonObject>($"/sessions/{_sessionId}/events", body, cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
    }

    public async Task PostHandLowered(string personaId, CancellationToken cancellationToken = default)
    {
        var body = new JsonObject
        {
            ["kind"] = "hand-raise",
            ["personaId"] = personaId,
            ["raised"] = false,
        };

        var response = await _http
            .PostAsJsonAsync<JsonObject>($"/sessions/{_sessionId}/events", body, cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
    }
}
