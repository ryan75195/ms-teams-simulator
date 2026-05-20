namespace MeetingSim.Api.Contracts;

public sealed record ReactionEventRequest(int Tile, string Emoji) : EventRequest;
