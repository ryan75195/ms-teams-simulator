using MeetingSim.Etl.Moderator;

namespace MeetingSim.Tests.Unit.Etl.Moderator;

[TestFixture]
public class EventPostBodyFactoryTests
{
    [Test]
    public void Should_map_chat_decision_to_chat_post_body()
    {
        var decision = new ModeratorDecision("chat", "anuj", "Why is that?", null, "skeptic probe");

        var body = EventPostBodyFactory.FromDecision(decision);

        Assert.That(body, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(body!["kind"]!.GetValue<string>(), Is.EqualTo("chat"));
            Assert.That(body["personaId"]!.GetValue<string>(), Is.EqualTo("anuj"));
            Assert.That(body["text"]!.GetValue<string>(), Is.EqualTo("Why is that?"));
            Assert.That(body.ContainsKey("durationMs"), Is.False);
            Assert.That(body.ContainsKey("raised"), Is.False);
        });
    }

    [Test]
    public void Should_map_hand_raise_decision_with_default_raised_true()
    {
        var decision = new ModeratorDecision("hand-raise", "serena", null, null, "wants the floor");

        var body = EventPostBodyFactory.FromDecision(decision);

        Assert.That(body, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(body!["kind"]!.GetValue<string>(), Is.EqualTo("hand-raise"));
            Assert.That(body["personaId"]!.GetValue<string>(), Is.EqualTo("serena"));
            Assert.That(body["raised"]!.GetValue<bool>(), Is.True);
        });
    }

    [Test]
    public void Should_respect_explicit_false_on_hand_raise()
    {
        var decision = new ModeratorDecision("hand-raise", "serena", null, false, "dropped");

        var body = EventPostBodyFactory.FromDecision(decision);

        Assert.That(body, Is.Not.Null);
        Assert.That(body!["raised"]!.GetValue<bool>(), Is.False);
    }

    [Test]
    public void Should_map_speak_decision_with_text_and_default_duration()
    {
        var decision = new ModeratorDecision("speak", "kayo", "How can we double down on this?", null, "cheering on");

        var body = EventPostBodyFactory.FromDecision(decision);

        Assert.That(body, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(body!["kind"]!.GetValue<string>(), Is.EqualTo("speak"));
            Assert.That(body["personaId"]!.GetValue<string>(), Is.EqualTo("kayo"));
            Assert.That(body["text"]!.GetValue<string>(), Is.EqualTo("How can we double down on this?"));
            Assert.That(body["durationMs"]!.GetValue<int>(), Is.EqualTo(EventPostBodyFactory.DefaultSpeakDurationMs));
        });
    }

    [Test]
    public void Should_return_null_for_none_action()
    {
        var decision = new ModeratorDecision("none", null, null, null, "no reaction warranted");

        var body = EventPostBodyFactory.FromDecision(decision);

        Assert.That(body, Is.Null);
    }

    [Test]
    public void Should_return_null_when_chat_is_missing_persona_or_text()
    {
        var noPersona = EventPostBodyFactory.FromDecision(
            new ModeratorDecision("chat", null, "text", null, "reason"));
        var noText = EventPostBodyFactory.FromDecision(
            new ModeratorDecision("chat", "anuj", null, null, "reason"));

        Assert.Multiple(() =>
        {
            Assert.That(noPersona, Is.Null);
            Assert.That(noText, Is.Null);
        });
    }

    [Test]
    public void Should_return_null_when_speak_or_hand_raise_is_missing_persona()
    {
        var speakNoPersona = EventPostBodyFactory.FromDecision(
            new ModeratorDecision("speak", null, "text", null, "reason"));
        var handNoPersona = EventPostBodyFactory.FromDecision(
            new ModeratorDecision("hand-raise", null, null, true, "reason"));

        Assert.Multiple(() =>
        {
            Assert.That(speakNoPersona, Is.Null);
            Assert.That(handNoPersona, Is.Null);
        });
    }

    [Test]
    public void Should_return_null_when_speak_is_missing_text()
    {
        var noText = EventPostBodyFactory.FromDecision(
            new ModeratorDecision("speak", "anuj", null, null, "reason"));

        Assert.That(noText, Is.Null);
    }

    [Test]
    public void Should_return_null_for_unknown_action()
    {
        var decision = new ModeratorDecision("teleport", "anuj", null, null, "broken");

        var body = EventPostBodyFactory.FromDecision(decision);

        Assert.That(body, Is.Null);
    }
}
