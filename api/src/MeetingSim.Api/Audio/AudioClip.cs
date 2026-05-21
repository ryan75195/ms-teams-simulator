namespace MeetingSim.Api.Audio;

public sealed record AudioClip(ReadOnlyMemory<byte> Bytes, string ContentType);
