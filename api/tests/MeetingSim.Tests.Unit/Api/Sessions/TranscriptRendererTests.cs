using System.Text.Json;
using MeetingSim.Api.Sessions;
using MeetingSim.Core.Events;
using MeetingSim.Core.Personas;
using MeetingSim.Core.Sessions;

namespace MeetingSim.Tests.Unit.Api.Sessions;

[TestFixture]
public class TranscriptRendererTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };


    [Test]
    public void Should_render_header_with_title_and_session_id()
    {
        var manifest = NewManifest("Q2 Sales Review");

        var markdown = TranscriptRenderer.Render(manifest, []);

        Assert.Multiple(() =>
        {
            Assert.That(markdown, Does.StartWith("# Q2 Sales Review"));
            Assert.That(markdown, Does.Contain(manifest.Id.ToString()));
            Assert.That(markdown, Does.Contain("Audience size: 50"));
        });
    }

    [Test]
    public void Should_include_ended_at_line_when_session_finalised()
    {
        var manifest = NewManifest() with { EndedAt = new DateTimeOffset(2026, 5, 24, 11, 30, 0, TimeSpan.Zero) };

        var markdown = TranscriptRenderer.Render(manifest, []);

        Assert.That(markdown, Does.Contain("Ended:"));
    }

    [Test]
    public void Should_omit_ended_at_line_for_active_session()
    {
        var manifest = NewManifest();

        var markdown = TranscriptRenderer.Render(manifest, []);

        Assert.That(markdown, Does.Not.Contain("Ended:"));
    }

    [Test]
    public void Should_render_a_transcript_chunk_as_presenter_quote()
    {
        var manifest = NewManifest();
        var line = Serialize(new TranscriptChunkEvent(1, manifest.StartedAt, "Pipeline is up 18%.", IsFinal: true));

        var markdown = TranscriptRenderer.Render(manifest, new[] { line });

        Assert.Multiple(() =>
        {
            Assert.That(markdown, Does.Contain("Presenter"));
            Assert.That(markdown, Does.Contain("> Pipeline is up 18%."));
        });
    }

    [Test]
    public void Should_render_a_speak_event_with_persona_display_name()
    {
        var manifest = NewManifest();
        var line = Serialize(new SpeakEvent(2, manifest.StartedAt, "anuj", "Where does the 18% come from?", 3000));

        var markdown = TranscriptRenderer.Render(manifest, new[] { line });

        Assert.Multiple(() =>
        {
            Assert.That(markdown, Does.Contain("Anuj Kapoor"));
            Assert.That(markdown, Does.Contain("*(speaks)*"));
            Assert.That(markdown, Does.Contain("> Where does the 18% come from?"));
        });
    }

    [Test]
    public void Should_render_a_chat_event_with_persona_display_name()
    {
        var manifest = NewManifest();
        var line = Serialize(new ChatMessageEvent(3, manifest.StartedAt, "anuj", "Quick question."));

        var markdown = TranscriptRenderer.Render(manifest, new[] { line });

        Assert.Multiple(() =>
        {
            Assert.That(markdown, Does.Contain("Anuj Kapoor"));
            Assert.That(markdown, Does.Contain("*(chat)*"));
            Assert.That(markdown, Does.Contain("> Quick question."));
        });
    }

    [Test]
    public void Should_render_hand_raise_as_raised_hand_marker()
    {
        var manifest = NewManifest();
        var line = Serialize(new HandRaiseEvent(4, manifest.StartedAt, "anuj", Raised: true));

        var markdown = TranscriptRenderer.Render(manifest, new[] { line });

        Assert.That(markdown, Does.Contain("✋ raised hand"));
    }

    [Test]
    public void Should_render_hand_lower_as_lowered_hand_marker()
    {
        var manifest = NewManifest();
        var line = Serialize(new HandRaiseEvent(5, manifest.StartedAt, "anuj", Raised: false));

        var markdown = TranscriptRenderer.Render(manifest, new[] { line });

        Assert.That(markdown, Does.Contain("✋ lowered hand"));
    }

    [Test]
    public void Should_render_reaction_with_emoji()
    {
        var manifest = NewManifest();
        var line = Serialize(new ReactionEvent(6, manifest.StartedAt, 7, "👍"));

        var markdown = TranscriptRenderer.Render(manifest, new[] { line });

        Assert.That(markdown, Does.Contain("reaction 👍"));
    }

    [Test]
    public void Should_render_slide_update_in_fenced_code_block()
    {
        var manifest = NewManifest();
        var line = Serialize(new SlideUpdateEvent(7, manifest.StartedAt, "Q2 Sales\n- EMEA +18.4%"));

        var markdown = TranscriptRenderer.Render(manifest, new[] { line });

        Assert.Multiple(() =>
        {
            Assert.That(markdown, Does.Contain("slide change"));
            Assert.That(markdown, Does.Contain("```"));
            Assert.That(markdown, Does.Contain("Q2 Sales"));
            Assert.That(markdown, Does.Contain("EMEA +18.4%"));
        });
    }

    [Test]
    public void Should_fall_back_to_persona_id_when_roster_does_not_include_the_speaker()
    {
        var manifest = NewManifest();
        var line = Serialize(new SpeakEvent(8, manifest.StartedAt, "unknown-id", "Hi.", 1000));

        var markdown = TranscriptRenderer.Render(manifest, new[] { line });

        Assert.That(markdown, Does.Contain("unknown-id"));
    }

    [Test]
    public void Should_skip_blank_lines_silently()
    {
        var manifest = NewManifest();
        var validLine = Serialize(new ChatMessageEvent(1, manifest.StartedAt, "anuj", "first"));

        var markdown = TranscriptRenderer.Render(manifest, new[] { "", "   ", validLine, "" });

        Assert.That(markdown, Does.Contain("> first"));
    }

    [Test]
    public void Should_skip_malformed_json_lines_silently()
    {
        var manifest = NewManifest();
        var validLine = Serialize(new ChatMessageEvent(1, manifest.StartedAt, "anuj", "good"));

        var markdown = TranscriptRenderer.Render(manifest, new[] { "not-json {{{", validLine });

        Assert.That(markdown, Does.Contain("> good"));
    }

    private static string Serialize(MeetingEvent evt) =>
        JsonSerializer.Serialize(evt, SerializerOptions);

    private static SessionManifest NewManifest(string title = "Test session") => new(
        Id: Guid.NewGuid(),
        Title: title,
        AudienceSize: 50,
        Seed: 12345,
        Settings: new SessionSettings(Engagement: 5, Noise: 2, AutoChat: true, AutoReactions: true),
        Roster: [new("anuj", "Anuj Kapoor", "#3F628E", Archetype.Skeptic, IsRoster: true)],
        StartedAt: new DateTimeOffset(2026, 5, 24, 10, 0, 0, TimeSpan.Zero),
        LastEventAt: null,
        EndedAt: null);
}
