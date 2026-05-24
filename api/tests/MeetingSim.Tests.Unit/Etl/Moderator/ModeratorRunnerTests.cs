using System.Net;
using MeetingSim.Etl.Moderator;

namespace MeetingSim.Tests.Unit.Etl.Moderator;

[TestFixture]
public class ModeratorRunnerTests
{
    [Test]
    public async Task Should_return_requested_session_id_when_explicit_one_is_passed()
    {
        var http = new HttpClient(new StubHandler("[]")) { BaseAddress = new Uri("http://localhost") };
        var explicitId = Guid.NewGuid();

        var resolved = await ModeratorRunner.ResolveSessionId(http, explicitId);

        Assert.That(resolved, Is.EqualTo(explicitId));
    }

    [Test]
    public async Task Should_return_null_when_no_active_sessions_and_no_explicit_id()
    {
        var http = new HttpClient(new StubHandler("[]")) { BaseAddress = new Uri("http://localhost") };

        var resolved = await ModeratorRunner.ResolveSessionId(http, requested: null);

        Assert.That(resolved, Is.Null);
    }

    [Test]
    public async Task Should_resolve_newest_session_when_multiple_exist()
    {
        var older = Guid.NewGuid();
        var newer = Guid.NewGuid();
        var json = "[" +
            "{\"id\":\"" + older + "\",\"startedAt\":\"2026-05-24T10:00:00+00:00\"}," +
            "{\"id\":\"" + newer + "\",\"startedAt\":\"2026-05-24T11:00:00+00:00\"}" +
            "]";
        var http = new HttpClient(new StubHandler(json)) { BaseAddress = new Uri("http://localhost") };

        var resolved = await ModeratorRunner.ResolveSessionId(http, requested: null);

        Assert.That(resolved, Is.EqualTo(newer));
    }

    [Test]
    public async Task Should_resolve_single_session_when_only_one_exists()
    {
        var id = Guid.NewGuid();
        var json = "[{\"id\":\"" + id + "\",\"startedAt\":\"2026-05-24T10:00:00+00:00\"}]";
        var http = new HttpClient(new StubHandler(json)) { BaseAddress = new Uri("http://localhost") };

        var resolved = await ModeratorRunner.ResolveSessionId(http, requested: null);

        Assert.That(resolved, Is.EqualTo(id));
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly string _responseBody;
        public StubHandler(string responseBody) { _responseBody = responseBody; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_responseBody, System.Text.Encoding.UTF8, "application/json"),
            });
        }
    }
}
