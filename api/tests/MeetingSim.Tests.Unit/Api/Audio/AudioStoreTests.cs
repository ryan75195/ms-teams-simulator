using MeetingSim.Api.Audio;

namespace MeetingSim.Tests.Unit.Api.Audio;

[TestFixture]
public class AudioStoreTests
{
    private static readonly byte[] SampleBytes = [0x49, 0x44, 0x33, 0x04];
    private const string SampleContentType = "audio/mpeg";

    [Test]
    public void Should_store_and_retrieve_a_clip_by_session_and_event_id()
    {
        var store = new AudioStore();
        var sessionId = Guid.NewGuid();
        var clip = new AudioClip(SampleBytes, SampleContentType);

        store.Put(sessionId, eventId: 42, clip);
        var fetched = store.TryGet(sessionId, eventId: 42);

        Assert.Multiple(() =>
        {
            Assert.That(fetched, Is.Not.Null);
            Assert.That(fetched!.Bytes.ToArray(), Is.EqualTo(SampleBytes));
            Assert.That(fetched.ContentType, Is.EqualTo(SampleContentType));
        });
    }

    [Test]
    public void Should_return_null_when_clip_is_missing()
    {
        var store = new AudioStore();

        var fetched = store.TryGet(Guid.NewGuid(), eventId: 1);

        Assert.That(fetched, Is.Null);
    }

    [Test]
    public void Should_keep_clips_isolated_between_sessions()
    {
        var store = new AudioStore();
        var sessionA = Guid.NewGuid();
        var sessionB = Guid.NewGuid();
        var clipA = new AudioClip(SampleBytes, SampleContentType);
        var clipB = new AudioClip(new byte[] { 0xFF, 0xFB }, SampleContentType);

        store.Put(sessionA, eventId: 1, clipA);
        store.Put(sessionB, eventId: 1, clipB);

        Assert.Multiple(() =>
        {
            Assert.That(store.TryGet(sessionA, 1)!.Bytes.ToArray(), Is.EqualTo(SampleBytes));
            Assert.That(store.TryGet(sessionB, 1)!.Bytes.ToArray(), Is.EqualTo(new byte[] { 0xFF, 0xFB }));
        });
    }

    [Test]
    public void Should_overwrite_an_existing_clip_on_put()
    {
        var store = new AudioStore();
        var sessionId = Guid.NewGuid();
        var first = new AudioClip(SampleBytes, SampleContentType);
        var second = new AudioClip(new byte[] { 0xAA, 0xBB }, SampleContentType);

        store.Put(sessionId, eventId: 7, first);
        store.Put(sessionId, eventId: 7, second);

        Assert.That(store.TryGet(sessionId, 7)!.Bytes.ToArray(), Is.EqualTo(new byte[] { 0xAA, 0xBB }));
    }

    [Test]
    public void Should_remove_a_clip_and_return_true_when_present()
    {
        var store = new AudioStore();
        var sessionId = Guid.NewGuid();
        store.Put(sessionId, eventId: 3, new AudioClip(SampleBytes, SampleContentType));

        var removed = store.Remove(sessionId, eventId: 3);

        Assert.Multiple(() =>
        {
            Assert.That(removed, Is.True);
            Assert.That(store.TryGet(sessionId, 3), Is.Null);
        });
    }

    [Test]
    public void Should_return_false_when_removing_unknown_clip()
    {
        var store = new AudioStore();

        var removed = store.Remove(Guid.NewGuid(), eventId: 9);

        Assert.That(removed, Is.False);
    }
}
