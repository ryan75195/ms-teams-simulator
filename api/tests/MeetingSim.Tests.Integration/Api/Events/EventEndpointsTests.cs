using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace MeetingSim.Tests.Integration.Api.Events;

[TestFixture]
public class EventEndpointsTests
{
    [Test]
    public async Task Should_append_a_chat_event_and_echo_the_persisted_payload()
    {
        await using var factory = new MeetingSimAppFactory();
        using var http = factory.CreateClient();
        var sessionId = await CreateSession(http);

        var response = await http.PostAsJsonAsync($"/sessions/{sessionId}/events", new
        {
            kind = "chat",
            personaId = "anuj",
            text = "Where does the 18% come from?",
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Multiple(() =>
        {
            Assert.That(body.GetProperty("kind").GetString(), Is.EqualTo("chat"));
            Assert.That(body.GetProperty("personaId").GetString(), Is.EqualTo("anuj"));
            Assert.That(body.GetProperty("text").GetString(), Is.EqualTo("Where does the 18% come from?"));
            Assert.That(body.GetProperty("id").GetInt64(), Is.GreaterThan(0));
        });
    }

    [Test]
    public async Task Should_append_a_hand_raise_event_with_raised_true()
    {
        await using var factory = new MeetingSimAppFactory();
        using var http = factory.CreateClient();
        var sessionId = await CreateSession(http);

        var response = await http.PostAsJsonAsync($"/sessions/{sessionId}/events", new
        {
            kind = "hand-raise",
            personaId = "bryan",
            raised = true,
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Multiple(() =>
        {
            Assert.That(body.GetProperty("kind").GetString(), Is.EqualTo("hand-raise"));
            Assert.That(body.GetProperty("personaId").GetString(), Is.EqualTo("bryan"));
            Assert.That(body.GetProperty("raised").GetBoolean(), Is.True);
        });
    }

    [Test]
    public async Task Should_append_a_reaction_event_with_emoji()
    {
        await using var factory = new MeetingSimAppFactory();
        using var http = factory.CreateClient();
        var sessionId = await CreateSession(http);

        var response = await http.PostAsJsonAsync($"/sessions/{sessionId}/events", new
        {
            kind = "reaction",
            tile = 7,
            emoji = "👍",
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Multiple(() =>
        {
            Assert.That(body.GetProperty("kind").GetString(), Is.EqualTo("reaction"));
            Assert.That(body.GetProperty("emoji").GetString(), Is.EqualTo("👍"));
            Assert.That(body.GetProperty("tile").GetInt32(), Is.EqualTo(7));
        });
    }

    [Test]
    public async Task Should_append_a_slide_update_event()
    {
        await using var factory = new MeetingSimAppFactory();
        using var http = factory.CreateClient();
        var sessionId = await CreateSession(http);

        var response = await http.PostAsJsonAsync($"/sessions/{sessionId}/events", new
        {
            kind = "slide-update",
            text = "Q2 Sales Report\n- EMEA +18.4%",
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(body.GetProperty("text").GetString(), Does.Contain("EMEA +18.4%"));
    }

    [Test]
    public async Task Should_append_a_silence_tick_event()
    {
        await using var factory = new MeetingSimAppFactory();
        using var http = factory.CreateClient();
        var sessionId = await CreateSession(http);

        var response = await http.PostAsJsonAsync($"/sessions/{sessionId}/events", new
        {
            kind = "silence-tick",
            seconds = 4,
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Multiple(() =>
        {
            Assert.That(body.GetProperty("kind").GetString(), Is.EqualTo("silence-tick"));
            Assert.That(body.GetProperty("seconds").GetInt32(), Is.EqualTo(4));
        });
    }

    [Test]
    public async Task Should_return_404_when_appending_to_an_unknown_session()
    {
        await using var factory = new MeetingSimAppFactory();
        using var http = factory.CreateClient();

        var response = await http.PostAsJsonAsync($"/sessions/{Guid.NewGuid()}/events", new
        {
            kind = "chat",
            personaId = "anuj",
            text = "Will not land.",
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Should_read_back_appended_events_via_since_query()
    {
        await using var factory = new MeetingSimAppFactory();
        using var http = factory.CreateClient();
        var sessionId = await CreateSession(http);
        await http.PostAsJsonAsync($"/sessions/{sessionId}/events", new { kind = "chat", personaId = "anuj", text = "first" });
        await http.PostAsJsonAsync($"/sessions/{sessionId}/events", new { kind = "chat", personaId = "ray", text = "second" });

        var response = await http.GetAsync($"/sessions/{sessionId}/events?since=0");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var texts = body.EnumerateArray()
            .Where(e => e.GetProperty("kind").GetString() == "chat")
            .Select(e => e.GetProperty("text").GetString())
            .ToList();
        Assert.That(texts, Is.EquivalentTo(new[] { "first", "second" }));
    }

    private static async Task<Guid> CreateSession(HttpClient http)
    {
        var response = await http.PostAsJsonAsync("/sessions", new { title = "Test", audienceSize = 20 });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("id").GetGuid();
    }
}
