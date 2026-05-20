namespace MeetingSim.Core.Sessions;

public sealed record SessionSettings(
    int Engagement,
    int Noise,
    bool AutoChat,
    bool AutoReactions);
