using System.Globalization;
using Microsoft.AspNetCore.SignalR;

namespace MeetingSim.Api.Events;

internal sealed class SessionHub : Hub
{
    public const string EventMethodName = "event";

    public Task JoinSession(Guid sessionId)
        => Groups.AddToGroupAsync(Context.ConnectionId, GroupName(sessionId));

    public Task LeaveSession(Guid sessionId)
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(sessionId));

    public static string GroupName(Guid sessionId)
        => "session-" + sessionId.ToString("N", CultureInfo.InvariantCulture);
}
