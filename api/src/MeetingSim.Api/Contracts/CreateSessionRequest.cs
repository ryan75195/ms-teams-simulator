using MeetingSim.Core.Sessions;

namespace MeetingSim.Api.Contracts;

public sealed record CreateSessionRequest(
    string Title,
    int AudienceSize,
    SessionSettings? Settings);
