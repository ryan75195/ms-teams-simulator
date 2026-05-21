namespace MeetingSim.Api.Contracts;

public sealed record SlideUpdateEventRequest(string Text) : EventRequest;
