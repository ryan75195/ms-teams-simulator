using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace MeetingSim.Tests.Integration.Api.Personas;

[TestFixture]
public class PersonaEndpointsTests
{
    [Test]
    public async Task Should_return_the_full_roster_when_count_is_omitted()
    {
        await using var factory = new MeetingSimAppFactory();
        using var http = factory.CreateClient();
        var sessionId = await CreateSession(http);

        var response = await http.GetAsync($"/sessions/{sessionId}/personas");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var roster = body.GetProperty("roster").EnumerateArray().ToList();
        Assert.That(roster, Is.Not.Empty);
    }

    [Test]
    public async Task Should_include_named_personas_in_the_roster()
    {
        await using var factory = new MeetingSimAppFactory();
        using var http = factory.CreateClient();
        var sessionId = await CreateSession(http);

        var response = await http.GetAsync($"/sessions/{sessionId}/personas?count=20");

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var ids = body.GetProperty("roster").EnumerateArray()
            .Select(p => p.GetProperty("id").GetString())
            .ToList();
        Assert.Multiple(() =>
        {
            Assert.That(ids, Does.Contain("anuj"));
            Assert.That(ids, Does.Contain("serena"));
            Assert.That(ids, Does.Contain("bryan"));
        });
    }

    [Test]
    public async Task Should_return_404_for_an_unknown_session()
    {
        await using var factory = new MeetingSimAppFactory();
        using var http = factory.CreateClient();

        var response = await http.GetAsync($"/sessions/{Guid.NewGuid()}/personas");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    private static async Task<Guid> CreateSession(HttpClient http)
    {
        var response = await http.PostAsJsonAsync("/sessions", new { title = "Test", audienceSize = 20 });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("id").GetGuid();
    }
}
