using System.Text.Json;
using MeetingSim.Core.Events;

namespace MeetingSim.Tests.Unit.Core.Events;

[TestFixture]
public class MeetingEventSerializationTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    [Test]
    public void Should_round_trip_a_speak_event_via_polymorphic_serialization()
    {
        MeetingEvent original = new SpeakEvent(42, DateTimeOffset.UnixEpoch, "anuj", "Hello there", 1200);

        var json = JsonSerializer.Serialize(original, Options);
        var decoded = JsonSerializer.Deserialize<MeetingEvent>(json, Options);

        Assert.That(decoded, Is.EqualTo(original));
        Assert.That(json, Does.Contain("\"kind\":\"speak\""));
    }

    [Test]
    public void Should_round_trip_a_hand_raise_event_via_polymorphic_serialization()
    {
        MeetingEvent original = new HandRaiseEvent(7, DateTimeOffset.UnixEpoch, "serena", Raised: true);

        var json = JsonSerializer.Serialize(original, Options);
        var decoded = JsonSerializer.Deserialize<MeetingEvent>(json, Options);

        Assert.That(decoded, Is.EqualTo(original));
        Assert.That(json, Does.Contain("\"kind\":\"hand-raise\""));
    }

    [Test]
    public void Should_round_trip_a_chat_message_event_via_polymorphic_serialization()
    {
        MeetingEvent original = new ChatMessageEvent(99, DateTimeOffset.UnixEpoch, "kayo", "+1 to that");

        var json = JsonSerializer.Serialize(original, Options);
        var decoded = JsonSerializer.Deserialize<MeetingEvent>(json, Options);

        Assert.That(decoded, Is.EqualTo(original));
        Assert.That(json, Does.Contain("\"kind\":\"chat\""));
    }

    [Test]
    public void Should_round_trip_a_reaction_event_via_polymorphic_serialization()
    {
        MeetingEvent original = new ReactionEvent(123, DateTimeOffset.UnixEpoch, Tile: 2, Emoji: "🎉");

        var json = JsonSerializer.Serialize(original, Options);
        var decoded = JsonSerializer.Deserialize<MeetingEvent>(json, Options);

        Assert.That(decoded, Is.EqualTo(original));
        Assert.That(json, Does.Contain("\"kind\":\"reaction\""));
    }
}
