using System.Net.Http.Json;
using System.Text.Json.Nodes;
using MeetingSim.Etl.Moderator.Interfaces;

namespace MeetingSim.Etl.Moderator;

internal sealed class ApiEventClient : IEventPoster
{
    private readonly HttpClient _http;
    private readonly Guid _sessionId;

    public ApiEventClient(HttpClient http, Guid sessionId)
    {
        _http = http;
        _sessionId = sessionId;
    }

    public async Task PostEvent(JsonObject body, CancellationToken cancellationToken = default)
    {
        var response = await _http
            .PostAsJsonAsync<JsonObject>($"/sessions/{_sessionId}/events", body, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }
}
