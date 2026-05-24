using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using MeetingSim.Api.Contracts;

namespace MeetingSim.Tests.Unit.Api.Contracts;

[TestFixture]
public class EventRequestTests
{
    public static IEnumerable<TestCaseData> Subtypes()
    {
        yield return new TestCaseData((EventRequest)new SpeakEventRequest("anuj", "What's the CAC?", 3000))
            .SetName("speak");
        yield return new TestCaseData((EventRequest)new HandRaiseEventRequest("bryan", Raised: true))
            .SetName("hand-raise");
        yield return new TestCaseData((EventRequest)new ChatMessageEventRequest("ray", "Nice point."))
            .SetName("chat");
        yield return new TestCaseData((EventRequest)new ReactionEventRequest(7, "👍"))
            .SetName("reaction");
        yield return new TestCaseData((EventRequest)new SlideUpdateEventRequest("Q2 Sales Report\n- EMEA +18.4%"))
            .SetName("slide-update");
        yield return new TestCaseData((EventRequest)new SilenceTickEventRequest(4))
            .SetName("silence-tick");
    }

    [TestCaseSource(nameof(Subtypes))]
    public void Should_roundtrip_every_event_request_subtype_through_polymorphic_json(EventRequest original)
    {
        var json = JsonSerializer.Serialize(original);
        var roundtripped = JsonSerializer.Deserialize<EventRequest>(json);

        Assert.That(roundtripped, Is.EqualTo(original));
    }

    [Test]
    public void Should_register_every_concrete_subtype_in_the_polymorphic_discriminator()
    {
        var registered = typeof(EventRequest)
            .GetCustomAttributes<JsonDerivedTypeAttribute>()
            .Select(a => a.DerivedType)
            .ToHashSet();

        var declared = typeof(EventRequest).Assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(EventRequest).IsAssignableFrom(t))
            .ToList();

        var missing = declared.Where(t => !registered.Contains(t)).Select(t => t.Name).ToList();
        Assert.That(missing, Is.Empty,
            "Every concrete EventRequest subtype must appear in [JsonDerivedType] on the base.");
    }

    [Test]
    public void Should_use_a_distinct_discriminator_string_for_every_registered_subtype()
    {
        var discriminators = typeof(EventRequest)
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
