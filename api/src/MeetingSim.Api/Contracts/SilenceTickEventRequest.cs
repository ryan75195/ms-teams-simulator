namespace MeetingSim.Api.Contracts;

public sealed record SilenceTickEventRequest(int Seconds) : EventRequest;
