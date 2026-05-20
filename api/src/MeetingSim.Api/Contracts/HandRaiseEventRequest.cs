namespace MeetingSim.Api.Contracts;

public sealed record HandRaiseEventRequest(string PersonaId, bool Raised) : EventRequest;
