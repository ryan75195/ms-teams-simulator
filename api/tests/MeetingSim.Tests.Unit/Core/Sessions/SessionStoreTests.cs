using MeetingSim.Core.Sessions;

namespace MeetingSim.Tests.Unit.Core.Sessions;

[TestFixture]
public class SessionStoreTests
{
    private static readonly SessionSettings DefaultSettings = new(
        Engagement: 5,
        Noise: 2,
        AutoChat: true,
        AutoReactions: true);

    [Test]
    public void Should_create_a_session_with_id_title_audience_and_settings()
    {
        var store = new SessionStore();

        var session = store.Create("Sales Report", 250, DefaultSettings);

        Assert.Multiple(() =>
        {
            Assert.That(session.Id, Is.Not.EqualTo(Guid.Empty));
            Assert.That(session.Title, Is.EqualTo("Sales Report"));
            Assert.That(session.AudienceSize, Is.EqualTo(250));
            Assert.That(session.Settings, Is.EqualTo(DefaultSettings));
            Assert.That(session.StartedAt, Is.GreaterThan(DateTimeOffset.UtcNow.AddSeconds(-5)));
        });
    }

    [Test]
    public void Should_create_sessions_with_unique_ids()
    {
        var store = new SessionStore();

        var first = store.Create("A", 10, DefaultSettings);
        var second = store.Create("A", 10, DefaultSettings);

        Assert.That(first.Id, Is.Not.EqualTo(second.Id));
    }

    [Test]
    public void Should_return_the_session_when_try_get_with_known_id()
    {
        var store = new SessionStore();
        var session = store.Create("Test", 100, DefaultSettings);

        var fetched = store.TryGet(session.Id);

        Assert.That(fetched, Is.EqualTo(session));
    }

    [Test]
    public void Should_return_null_when_try_get_with_unknown_id()
    {
        var store = new SessionStore();
        store.Create("Test", 100, DefaultSettings);

        var fetched = store.TryGet(Guid.NewGuid());

        Assert.That(fetched, Is.Null);
    }

    [Test]
    public void Should_list_every_stored_session()
    {
        var store = new SessionStore();
        var first = store.Create("One", 10, DefaultSettings);
        var second = store.Create("Two", 20, DefaultSettings);

        var all = store.List();

        Assert.Multiple(() =>
        {
            Assert.That(all, Has.Count.EqualTo(2));
            Assert.That(all, Does.Contain(first));
            Assert.That(all, Does.Contain(second));
        });
    }

    [Test]
    public void Should_list_empty_for_a_fresh_store()
    {
        var store = new SessionStore();

        var all = store.List();

        Assert.That(all, Is.Empty);
    }

    [Test]
    public void Should_remove_and_return_true_when_id_is_known()
    {
        var store = new SessionStore();
        var session = store.Create("Test", 100, DefaultSettings);

        var removed = store.Remove(session.Id);

        Assert.Multiple(() =>
        {
            Assert.That(removed, Is.True);
            Assert.That(store.TryGet(session.Id), Is.Null);
        });
    }

    [Test]
    public void Should_return_false_when_removing_unknown_id()
    {
        var store = new SessionStore();

        var removed = store.Remove(Guid.NewGuid());

        Assert.That(removed, Is.False);
    }

    [Test]
    public void Should_replace_settings_and_preserve_identity_on_update()
    {
        var store = new SessionStore();
        var session = store.Create("Test", 100, DefaultSettings);
        var newSettings = new SessionSettings(Engagement: 9, Noise: 5, AutoChat: false, AutoReactions: false);

        var updated = store.Update(session.Id, newSettings);

        Assert.Multiple(() =>
        {
            Assert.That(updated, Is.Not.Null);
            Assert.That(updated!.Id, Is.EqualTo(session.Id));
            Assert.That(updated.Title, Is.EqualTo(session.Title));
            Assert.That(updated.Settings, Is.EqualTo(newSettings));
            Assert.That(store.TryGet(session.Id)!.Settings, Is.EqualTo(newSettings));
        });
    }

    [Test]
    public void Should_return_null_when_updating_unknown_id()
    {
        var store = new SessionStore();

        var updated = store.Update(Guid.NewGuid(), DefaultSettings);

        Assert.That(updated, Is.Null);
    }
}
