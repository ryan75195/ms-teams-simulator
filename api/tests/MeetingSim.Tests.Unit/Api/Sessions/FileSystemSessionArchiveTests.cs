using MeetingSim.Api.Sessions;
using MeetingSim.Core.Events;
using MeetingSim.Core.Personas;
using MeetingSim.Core.Sessions;
using Microsoft.Extensions.Configuration;

namespace MeetingSim.Tests.Unit.Api.Sessions;

[TestFixture]
public class FileSystemSessionArchiveTests
{
    private string _root = string.Empty;

    [SetUp]
    public void CreateTempRoot()
    {
        _root = Path.Combine(Path.GetTempPath(), $"archive-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
    }

    [TearDown]
    public void DeleteTempRoot()
    {
        if (Directory.Exists(_root))
        {
            try { Directory.Delete(_root, recursive: true); }
            catch (IOException) { }
        }
    }

    private FileSystemSessionArchive NewArchive()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["SessionArchive:Root"] = _root })
            .Build();
        return new FileSystemSessionArchive(config);
    }

    [Test]
    public async Task Should_save_and_load_a_manifest_at_the_configured_root()
    {
        var archive = NewArchive();
        var manifest = NewManifest();

        await archive.SaveManifest(manifest);
        var loaded = await archive.LoadManifest(manifest.Id);

        Assert.That(loaded, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(loaded!.Id, Is.EqualTo(manifest.Id));
            Assert.That(loaded.Title, Is.EqualTo(manifest.Title));
            Assert.That(loaded.AudienceSize, Is.EqualTo(manifest.AudienceSize));
            Assert.That(loaded.Seed, Is.EqualTo(manifest.Seed));
            Assert.That(loaded.Settings, Is.EqualTo(manifest.Settings));
            Assert.That(loaded.Roster, Has.Count.EqualTo(manifest.Roster.Count));
            Assert.That(loaded.Roster[0].Id, Is.EqualTo(manifest.Roster[0].Id));
            Assert.That(loaded.StartedAt, Is.EqualTo(manifest.StartedAt));
        });
    }

    [Test]
    public async Task Should_return_null_when_loading_a_missing_manifest()
    {
        var archive = NewArchive();

        var loaded = await archive.LoadManifest(Guid.NewGuid());

        Assert.That(loaded, Is.Null);
    }

    [Test]
    public async Task Should_write_manifest_to_session_directory_under_root()
    {
        var archive = NewArchive();
        var manifest = NewManifest();

        await archive.SaveManifest(manifest);

        var expected = Path.Combine(_root, manifest.Id.ToString(), "manifest.json");
        Assert.That(File.Exists(expected), Is.True, $"Expected manifest at {expected}");
    }

    [Test]
    public async Task Should_append_events_one_per_line_to_events_jsonl()
    {
        var archive = NewArchive();
        var sessionId = Guid.NewGuid();
        var first = new ChatMessageEvent(1, DateTimeOffset.UtcNow, "anuj", "first");
        var second = new ChatMessageEvent(2, DateTimeOffset.UtcNow, "ray", "second");

        await archive.AppendEvent(sessionId, first);
        await archive.AppendEvent(sessionId, second);

        var path = Path.Combine(_root, sessionId.ToString(), "events.jsonl");
        var lines = await File.ReadAllLinesAsync(path);
        Assert.Multiple(() =>
        {
            Assert.That(lines, Has.Length.EqualTo(2));
            Assert.That(lines[0], Does.Contain("\"kind\":\"chat\""));
            Assert.That(lines[0], Does.Contain("\"text\":\"first\""));
            Assert.That(lines[1], Does.Contain("\"text\":\"second\""));
        });
    }

    [Test]
    public async Task Should_read_back_appended_events_as_lines()
    {
        var archive = NewArchive();
        var sessionId = Guid.NewGuid();
        await archive.AppendEvent(sessionId, new ChatMessageEvent(1, DateTimeOffset.UtcNow, "anuj", "first"));
        await archive.AppendEvent(sessionId, new HandRaiseEvent(2, DateTimeOffset.UtcNow, "bryan", true));

        var lines = new List<string>();
        await foreach (var line in archive.ReadEventLines(sessionId))
        {
            lines.Add(line);
        }

        Assert.That(lines, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task Should_write_audio_bytes_to_event_id_path_under_audio_folder()
    {
        var archive = NewArchive();
        var sessionId = Guid.NewGuid();
        var payload = new byte[] { 0x89, 0x50, 0x4E, 0x47 };

        await archive.WriteAudio(sessionId, eventId: 42, payload);

        var path = Path.Combine(_root, sessionId.ToString(), "audio", "42.mp3");
        Assert.That(File.Exists(path), Is.True);
        var actual = await File.ReadAllBytesAsync(path);
        Assert.That(actual, Is.EqualTo(payload));
    }

    [Test]
    public async Task Should_open_audio_stream_for_existing_eventid()
    {
        var archive = NewArchive();
        var sessionId = Guid.NewGuid();
        var payload = new byte[] { 1, 2, 3 };
        await archive.WriteAudio(sessionId, eventId: 7, payload);

        await using var stream = await archive.OpenAudio(sessionId, eventId: 7);
        using var ms = new MemoryStream();
        if (stream is not null)
        {
            await stream.CopyToAsync(ms);
        }

        Assert.That(ms.ToArray(), Is.EqualTo(payload));
    }

    [Test]
    public async Task Should_return_null_audio_stream_when_eventid_missing()
    {
        var archive = NewArchive();

        var stream = await archive.OpenAudio(Guid.NewGuid(), eventId: 99);

        Assert.That(stream, Is.Null);
    }

    [Test]
    public async Task Should_list_archived_sessions_after_manifest_save()
    {
        var archive = NewArchive();
        var first = NewManifest();
        var second = NewManifest();
        await archive.SaveManifest(first);
        await archive.SaveManifest(second);

        var ids = archive.ListArchivedSessions();

        Assert.That(ids, Is.EquivalentTo(new[] { first.Id, second.Id }));
    }

    [Test]
    public void Should_return_empty_list_when_root_has_no_sessions()
    {
        var archive = NewArchive();

        var ids = archive.ListArchivedSessions();

        Assert.That(ids, Is.Empty);
    }

    [Test]
    public void Should_expose_session_directory_path()
    {
        var archive = NewArchive();
        var sessionId = Guid.NewGuid();

        var dir = archive.GetSessionDirectory(sessionId);

        Assert.That(dir, Is.EqualTo(Path.Combine(_root, sessionId.ToString())));
    }

    [Test]
    public async Task Should_append_decision_one_per_line_to_decisions_jsonl()
    {
        var archive = NewArchive();
        var sessionId = Guid.NewGuid();
        var decision = new System.Text.Json.Nodes.JsonObject
        {
            ["ts"] = "2026-05-24T12:00:00Z",
            ["reasoning"] = "test",
        };

        await archive.AppendDecision(sessionId, decision);

        var path = Path.Combine(_root, sessionId.ToString(), "decisions.jsonl");
        var lines = await File.ReadAllLinesAsync(path);
        Assert.Multiple(() =>
        {
            Assert.That(lines, Has.Length.EqualTo(1));
            Assert.That(lines[0], Does.Contain("\"reasoning\":\"test\""));
        });
    }

    private static SessionManifest NewManifest() => new(
        Id: Guid.NewGuid(),
        Title: "Test session",
        AudienceSize: 50,
        Seed: 12345,
        Settings: new SessionSettings(Engagement: 5, Noise: 2, AutoChat: true, AutoReactions: true),
        Roster: [new("anuj", "Anuj Kapoor", "#3F628E", Archetype.Skeptic, IsRoster: true)],
        StartedAt: new DateTimeOffset(2026, 5, 24, 10, 0, 0, TimeSpan.Zero),
        LastEventAt: null,
        EndedAt: null);
}
