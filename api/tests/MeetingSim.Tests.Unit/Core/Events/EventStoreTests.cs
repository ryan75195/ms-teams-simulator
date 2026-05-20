using MeetingSim.Core.Events;

namespace MeetingSim.Tests.Unit.Core.Events;

[TestFixture]
public class EventStoreTests
{
    private static SpeakEvent SpeakFactory(long id, DateTimeOffset ts)
        => new(id, ts, "anuj", 1200);

    [Test]
    public void Should_assign_monotonic_ids_on_append()
    {
        var store = new EventStore();
        var sessionId = Guid.NewGuid();

        var first = store.Append(sessionId, SpeakFactory);
        var second = store.Append(sessionId, SpeakFactory);
        var third = store.Append(sessionId, SpeakFactory);

        Assert.Multiple(() =>
        {
            Assert.That(first.Id, Is.EqualTo(1));
            Assert.That(second.Id, Is.EqualTo(2));
            Assert.That(third.Id, Is.EqualTo(3));
        });
    }

    [Test]
    public void Should_assign_a_recent_timestamp_on_append()
    {
        var store = new EventStore();
        var sessionId = Guid.NewGuid();

        var appended = store.Append(sessionId, SpeakFactory);

        Assert.That(appended.Ts, Is.GreaterThan(DateTimeOffset.UtcNow.AddSeconds(-5)));
    }

    [Test]
    public void Should_read_only_events_with_id_greater_than_since()
    {
        var store = new EventStore();
        var sessionId = Guid.NewGuid();
        store.Append(sessionId, SpeakFactory);
        store.Append(sessionId, SpeakFactory);
        store.Append(sessionId, SpeakFactory);

        var afterFirst = store.ReadSince(sessionId, sinceId: 1);

        Assert.Multiple(() =>
        {
            Assert.That(afterFirst, Has.Count.EqualTo(2));
            Assert.That(afterFirst[0].Id, Is.EqualTo(2));
            Assert.That(afterFirst[1].Id, Is.EqualTo(3));
        });
    }

    [Test]
    public void Should_read_empty_when_session_is_unknown()
    {
        var store = new EventStore();

        var read = store.ReadSince(Guid.NewGuid(), sinceId: 0);

        Assert.That(read, Is.Empty);
    }

    [Test]
    public void Should_keep_ids_isolated_between_sessions()
    {
        var store = new EventStore();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        var fromA = store.Append(a, SpeakFactory);
        var fromB = store.Append(b, SpeakFactory);

        Assert.Multiple(() =>
        {
            Assert.That(fromA.Id, Is.EqualTo(1));
            Assert.That(fromB.Id, Is.EqualTo(1));
            Assert.That(store.ReadSince(a, 0), Has.Count.EqualTo(1));
            Assert.That(store.ReadSince(b, 0), Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void Should_trim_the_ring_buffer_to_max_events_per_session()
    {
        var store = new EventStore();
        var sessionId = Guid.NewGuid();
        var totalAppended = EventStore.MaxEventsPerSession + 50;

        MeetingEvent? lastAppended = null;
        for (var i = 0; i < totalAppended; i++)
        {
            lastAppended = store.Append(sessionId, SpeakFactory);
        }

        var read = store.ReadSince(sessionId, sinceId: 0);

        Assert.Multiple(() =>
        {
            Assert.That(read, Has.Count.EqualTo(EventStore.MaxEventsPerSession));
            Assert.That(read[^1].Id, Is.EqualTo(lastAppended!.Id));
            Assert.That(read[0].Id, Is.EqualTo(totalAppended - EventStore.MaxEventsPerSession + 1));
        });
    }

    [Test]
    public void Should_drop_a_session_buffer_on_clear()
    {
        var store = new EventStore();
        var sessionId = Guid.NewGuid();
        store.Append(sessionId, SpeakFactory);

        store.Clear(sessionId);

        Assert.That(store.ReadSince(sessionId, 0), Is.Empty);
    }
}
