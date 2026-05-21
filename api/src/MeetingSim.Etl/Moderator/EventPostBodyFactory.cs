using System.Text.Json.Nodes;

namespace MeetingSim.Etl.Moderator;

internal static class EventPostBodyFactory
{
    public const int DefaultSpeakDurationMs = 3000;

    public static JsonObject? FromDecision(ModeratorDecision decision)
    {
        return decision.Action switch
        {
            "chat" when decision.PersonaId is { Length: > 0 } pid && decision.Text is { Length: > 0 } text
                => new JsonObject
                {
                    ["kind"] = "chat",
                    ["personaId"] = pid,
                    ["text"] = text,
                },
            "hand-raise" when decision.PersonaId is { Length: > 0 } pid
                => new JsonObject
                {
                    ["kind"] = "hand-raise",
                    ["personaId"] = pid,
                    ["raised"] = decision.Raised ?? true,
                },
            "speak" when decision.PersonaId is { Length: > 0 } pid && decision.Text is { Length: > 0 } text
                => new JsonObject
                {
                    ["kind"] = "speak",
                    ["personaId"] = pid,
                    ["text"] = text,
                    ["durationMs"] = DefaultSpeakDurationMs,
                },
            _ => null,
        };
    }
}
