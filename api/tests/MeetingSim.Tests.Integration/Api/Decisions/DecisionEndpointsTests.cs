using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace MeetingSim.Tests.Integration.Api.Decisions;

[TestFixture]
public class DecisionEndpointsTests
{
    [Test]
    public async Task Should_accept_a_decision_record_for_a_known_session()
    {
        await using var factory = new MeetingSimAppFactory();
        using var http = factory.CreateClient();
        var sessionId = await CreateSession(http);

        var decision = new
        {
            ts = DateTimeOffset.UtcNow.ToString("O"),
            presenterLine = "Hello.",
            state = new { activeResponderId = (string?)null, handsUp = Array.Empty<string>(), recentSpeakers = Array.Empty<string>(), hasSlide = false },
            reasoning = "no-op",
            toolCalls = Array.Empty<object>(),
        };
        var response = await http.PostAsJsonAsync($"/sessions/{sessionId}/decisions", decision);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
    }

    [Test]
    public async Task Should_return_404_for_decisions_on_an_unknown_session()
    {
        await using var factory = new MeetingSimAppFactory();
        using var http = factory.CreateClient();

        var decision = new { ts = DateTimeOffset.UtcNow.ToString("O"), presenterLine = "x" };
        var response = await http.PostAsJsonAsync($"/sessions/{Guid.NewGuid()}/decisions", decision);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    private static async Task<Guid> CreateSession(HttpClient http)
    {
        var response = await http.PostAsJsonAsync("/sessions", new { title = "Test", audienceSize = 10 });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("id").GetGuid();
    }
}
