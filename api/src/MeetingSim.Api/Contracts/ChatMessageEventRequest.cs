namespace MeetingSim.Api.Contracts;

public sealed record ChatMessageEventRequest(string PersonaId, string Text) : EventRequest;
