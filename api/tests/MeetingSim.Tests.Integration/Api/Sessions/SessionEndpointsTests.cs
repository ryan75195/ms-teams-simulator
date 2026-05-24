using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace MeetingSim.Tests.Integration.Api.Sessions;

[TestFixture]
public class SessionEndpointsTests
{
    [Test]
    public async Task Should_create_a_session_with_201_and_return_an_id()
    {
        await using var factory = new MeetingSimAppFactory();
        using var http = factory.CreateClient();

        var response = await http.PostAsJsonAsync("/sessions", new { title = "Q3 review", audienceSize = 50 });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Multiple(() =>
        {
            Assert.That(body.GetProperty("id").GetGuid(), Is.Not.EqualTo(Guid.Empty));
            Assert.That(body.GetProperty("title").GetString(), Is.EqualTo("Q3 review"));
            Assert.That(body.GetProperty("audienceSize").GetInt32(), Is.EqualTo(50));
        });
    }

    [Test]
    public async Task Should_return_the_session_by_id()
    {
        await using var factory = new MeetingSimAppFactory();
        using var http = factory.CreateClient();
        var created = await CreateSession(http, "Pipeline review", 80);

        var response = await http.GetAsync($"/sessions/{created}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(body.GetProperty("id").GetGuid(), Is.EqualTo(created));
    }

    [Test]
    public async Task Should_return_404_for_an_unknown_session()
    {
        await using var factory = new MeetingSimAppFactory();
        using var http = factory.CreateClient();

        var response = await http.GetAsync($"/sessions/{Guid.NewGuid()}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Should_list_created_sessions()
    {
        await using var factory = new MeetingSimAppFactory();
        using var http = factory.CreateClient();
        var first = await CreateSession(http, "A", 10);
        var second = await CreateSession(http, "B", 20);

        var response = await http.GetAsync("/sessions");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var ids = body.EnumerateArray().Select(s => s.GetProperty("id").GetGuid()).ToList();
        Assert.That(ids, Does.Contain(first).And.Contain(second));
    }

    [Test]
    public async Task Should_delete_a_session_and_subsequent_get_returns_404()
    {
        await using var factory = new MeetingSimAppFactory();
        using var http = factory.CreateClient();
        var created = await CreateSession(http, "Throwaway", 10);

        var delete = await http.DeleteAsync($"/sessions/{created}");
        Assert.That(delete.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        var get = await http.GetAsync($"/sessions/{created}");
        Assert.That(get.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    private static async Task<Guid> CreateSession(HttpClient http, string title, int audienceSize)
    {
        var response = await http.PostAsJsonAsync("/sessions", new { title, audienceSize });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("id").GetGuid();
    }
}
