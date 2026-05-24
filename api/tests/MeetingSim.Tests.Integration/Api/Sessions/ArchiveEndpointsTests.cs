using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace MeetingSim.Tests.Integration.Api.Sessions;

[TestFixture]
public class ArchiveEndpointsTests
{
    [Test]
    public async Task Should_list_archived_sessions_after_one_is_deleted()
    {
        await using var factory = new MeetingSimAppFactory();
        using var http = factory.CreateClient();
        var sessionId = await CreateSession(http);
        await http.PostAsJsonAsync($"/sessions/{sessionId}/events", new { kind = "chat", personaId = "anuj", text = "hello" });
        var delete = await http.DeleteAsync($"/sessions/{sessionId}");
        delete.EnsureSuccessStatusCode();

        var response = await http.GetAsync("/sessions/archived");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var ids = body.EnumerateArray().Select(s => s.GetProperty("id").GetGuid()).ToList();
        Assert.That(ids, Does.Contain(sessionId));
    }

    [Test]
    public async Task Should_render_a_transcript_markdown_for_an_archived_session()
    {
        await using var factory = new MeetingSimAppFactory();
        using var http = factory.CreateClient();
        var sessionId = await CreateSession(http);
        await http.PostAsJsonAsync($"/sessions/{sessionId}/events", new { kind = "chat", personaId = "anuj", text = "What's the CAC?" });

        var response = await http.GetAsync($"/sessions/{sessionId}/transcript.md");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var markdown = await response.Content.ReadAsStringAsync();
        Assert.That(markdown, Does.Contain("What's the CAC?"));
    }

    [Test]
    public async Task Should_return_zip_archive_for_a_session()
    {
        await using var factory = new MeetingSimAppFactory();
        using var http = factory.CreateClient();
        var sessionId = await CreateSession(http);
        await http.PostAsJsonAsync($"/sessions/{sessionId}/events", new { kind = "chat", personaId = "anuj", text = "hello" });

        var response = await http.GetAsync($"/sessions/{sessionId}/archive.zip");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/zip"));
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.That(bytes.Length, Is.GreaterThan(0));
    }

    [Test]
    public async Task Should_return_404_for_unknown_archived_session_transcript()
    {
        await using var factory = new MeetingSimAppFactory();
        using var http = factory.CreateClient();

        var response = await http.GetAsync($"/sessions/{Guid.NewGuid()}/transcript.md");

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
