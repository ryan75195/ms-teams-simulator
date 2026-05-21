namespace MeetingSim.Api.Realtime;

public sealed record TranscriptionEvent(bool IsFinal, string Text);
