using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using MeetingSim.Core.Events;

namespace MeetingSim.Tests.Unit.Core.Events;

[TestFixture]
public class MeetingEventTests
{
    private static readonly DateTimeOffset SampleTs = new(2026, 5, 24, 12, 0, 0, TimeSpan.Zero);

    public static IEnumerable<TestCaseData> Subtypes()
    {
        yield return new TestCaseData((MeetingEvent)new SpeakEvent(1, SampleTs, "anuj", "What's the CAC?", 3000))
            .SetName("speak");
        yield return new TestCaseData((MeetingEvent)new HandRaiseEvent(2, SampleTs, "bryan", true))
            .SetName("hand-raise");
        yield return new TestCaseData((MeetingEvent)new ChatMessageEvent(3, SampleTs, "ray", "Nice point."))
            .SetName("chat");
        yield return new TestCaseData((MeetingEvent)new ReactionEvent(4, SampleTs, 7, "👍"))
            .SetName("reaction");
        yield return new TestCaseData((MeetingEvent)new TranscriptChunkEvent(5, SampleTs, "Pipeline is up 18%.", IsFinal: true))
            .SetName("transcript");
        yield return new TestCaseData((MeetingEvent)new TranscriptMilestoneEvent(6, SampleTs, "Our pipeline grew eighteen percent this quarter"))
            .SetName("transcript-milestone");
        yield return new TestCaseData((MeetingEvent)new SlideUpdateEvent(7, SampleTs, "Q2 Sales Report\n- EMEA +18.4%"))
            .SetName("slide-update");
        yield return new TestCaseData((MeetingEvent)new SilenceTickEvent(8, SampleTs, 4))
            .SetName("silence-tick");
    }

    [TestCaseSource(nameof(Subtypes))]
    public void Should_roundtrip_every_meeting_event_subtype_through_polymorphic_json(MeetingEvent original)
    {
        var json = JsonSerializer.Serialize(original);
        var roundtripped = JsonSerializer.Deserialize<MeetingEvent>(json);

        Assert.That(roundtripped, Is.EqualTo(original));
    }

    [Test]
    public void Should_register_every_concrete_subtype_in_the_polymorphic_discriminator()
    {
        var registered = typeof(MeetingEvent)
            .GetCustomAttributes<JsonDerivedTypeAttribute>()
            .Select(a => a.DerivedType)
            .ToHashSet();

        var declared = typeof(MeetingEvent).Assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(MeetingEvent).IsAssignableFrom(t))
            .ToList();

        var missing = declared.Where(t => !registered.Contains(t)).Select(t => t.Name).ToList();
        Assert.That(missing, Is.Empty,
            "Every concrete MeetingEvent subtype must appear in [JsonDerivedType] on the base.");
    }

    [Test]
    public void Should_use_a_distinct_discriminator_string_for_every_registered_subtype()
    {
        var discriminators = typeof(MeetingEvent)
            .GetCustomAttributes<JsonDerivedTypeAttribute>()
            .Select(a => a.TypeDiscriminator?.ToString() ?? string.Empty)
            .ToList();

        Assert.Multiple(() =>
        {
            Assert.That(discriminators, Is.All.Not.Empty);
            Assert.That(discriminators.Distinct().Count(), Is.EqualTo(discriminators.Count));
        });
    }
}
