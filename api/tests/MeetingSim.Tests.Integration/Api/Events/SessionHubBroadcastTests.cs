using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;

namespace MeetingSim.Tests.Integration.Api.Events;

[TestFixture]
public class SessionHubBroadcastTests
{
    private const string SessionsRoute = "/sessions";
    private const string HubRoute = "/hubs/session";
    private static readonly TimeSpan BroadcastTimeout = TimeSpan.FromSeconds(5);

    [Test]
    public async Task Should_broadcast_appended_events_to_clients_joined_to_the_session_group()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var http = factory.CreateClient();

        var sessionId = await CreateSessionAsync(http);
        var receivedSignal = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var connection = BuildHubConnection(factory);
        connection.On<JsonElement>("event", evt => receivedSignal.TrySetResult(evt));

        await connection.StartAsync();
        await connection.InvokeAsync("JoinSession", sessionId);

        var chatRequest = new { kind = "chat", personaId = "anuj", text = "Test broadcast" };
        var post = await http.PostAsJsonAsync($"/sessions/{sessionId}/events", chatRequest);
        post.EnsureSuccessStatusCode();
        var posted = await post.Content.ReadFromJsonAsync<JsonElement>();

        var completed = await Task.WhenAny(receivedSignal.Task, Task.Delay(BroadcastTimeout));
        Assert.That(completed, Is.SameAs(receivedSignal.Task), "Did not receive event within timeout");

        var payload = await receivedSignal.Task;
        Assert.Multiple(() =>
        {
            Assert.That(payload.GetProperty("kind").GetString(), Is.EqualTo("chat"));
            Assert.That(payload.GetProperty("personaId").GetString(), Is.EqualTo("anuj"));
            Assert.That(payload.GetProperty("text").GetString(), Is.EqualTo("Test broadcast"));
            Assert.That(payload.GetProperty("id").GetInt64(), Is.EqualTo(posted.GetProperty("id").GetInt64()));
        });
    }

    [Test]
    public async Task Should_not_broadcast_to_clients_that_did_not_join_the_session()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var http = factory.CreateClient();

        var subscribedSessionId = await CreateSessionAsync(http);
        var otherSessionId = await CreateSessionAsync(http);
        var unexpected = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var connection = BuildHubConnection(factory);
        connection.On<JsonElement>("event", evt => unexpected.TrySetResult(evt));

        await connection.StartAsync();
        await connection.InvokeAsync("JoinSession", subscribedSessionId);

        var chatRequest = new { kind = "chat", personaId = "anuj", text = "Should not reach the other client" };
        var post = await http.PostAsJsonAsync($"/sessions/{otherSessionId}/events", chatRequest);
        post.EnsureSuccessStatusCode();

        var settled = await Task.WhenAny(unexpected.Task, Task.Delay(TimeSpan.FromMilliseconds(1500)));
        Assert.That(settled, Is.Not.SameAs(unexpected.Task), "Received an event meant for a different session");
    }

    private static async Task<Guid> CreateSessionAsync(HttpClient http)
    {
        var sessionRequest = new { title = "Broadcast Test", audienceSize = 50 };
        var response = await http.PostAsJsonAsync(SessionsRoute, sessionRequest);
        response.EnsureSuccessStatusCode();
        var session = await response.Content.ReadFromJsonAsync<JsonElement>();
        return session.GetProperty("id").GetGuid();
    }

    private static HubConnection BuildHubConnection(WebApplicationFactory<Program> factory)
    {
        return new HubConnectionBuilder()
            .WithUrl(new Uri(factory.Server.BaseAddress, HubRoute), options =>
            {
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
            })
            .Build();
    }
}
