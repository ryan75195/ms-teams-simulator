namespace MeetingSim.Api.Contracts;

public sealed record SpeakEventRequest(string PersonaId, string Text, int DurationMs) : EventRequest;
