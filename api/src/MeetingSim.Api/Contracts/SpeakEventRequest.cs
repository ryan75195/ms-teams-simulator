namespace MeetingSim.Api.Contracts;

public sealed record SpeakEventRequest(string PersonaId, int DurationMs) : EventRequest;
