using System.Text;
using System.Text.Json;
using MeetingSim.Core.Events;

namespace MeetingSim.Api.Sessions;

internal static class TranscriptRenderer
{
    private static readonly JsonSerializerOptions LineOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static string Render(SessionManifest manifest, IEnumerable<string> eventLines)
    {
        var sb = new StringBuilder();
        sb.Append("# ").AppendLine(manifest.Title);
        sb.Append("- Session id: `").Append(manifest.Id).AppendLine("`");
        sb.Append("- Started: ").AppendLine(manifest.StartedAt.ToString("u"));
        if (manifest.EndedAt is { } endedAt)
        {
            sb.Append("- Ended: ").AppendLine(endedAt.ToString("u"));
        }
        sb.Append("- Audience size: ").Append(manifest.AudienceSize).AppendLine();
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();

        foreach (var line in eventLines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }
            AppendEventLine(sb, line, manifest);
        }
        return sb.ToString();
    }

    private static void AppendEventLine(StringBuilder sb, string line, SessionManifest manifest)
    {
        MeetingEvent? evt;
        try
        {
            evt = JsonSerializer.Deserialize<MeetingEvent>(line, LineOptions);
        }
        catch (JsonException)
        {
            return;
        }
        if (evt is null)
        {
            return;
        }
        var stamp = evt.Ts.ToString("HH:mm:ss");
        AppendByKind(sb, stamp, evt, manifest);
    }

    private static void AppendByKind(StringBuilder sb, string stamp, MeetingEvent evt, SessionManifest manifest)
    {
        switch (evt)
        {
            case TranscriptChunkEvent t: AppendQuoted(sb, stamp, "Presenter", t.Text); break;
            case SpeakEvent s: AppendQuoted(sb, stamp, $"{DisplayName(s.PersonaId, manifest)} *(speaks)*", s.Text); break;
            case ChatMessageEvent c: AppendQuoted(sb, stamp, $"{DisplayName(c.PersonaId, manifest)} *(chat)*", c.Text); break;
            case HandRaiseEvent h: AppendHand(sb, stamp, DisplayName(h.PersonaId, manifest), h.Raised); break;
            case ReactionEvent r: AppendReaction(sb, stamp, r.Emoji); break;
            case SlideUpdateEvent u: AppendSlide(sb, stamp, u.Text); break;
        }
    }

    private static void AppendQuoted(StringBuilder sb, string stamp, string who, string text)
    {
        sb.Append("**").Append(stamp).Append("** — ").AppendLine(who)
          .Append("> ").AppendLine(text)
          .AppendLine();
    }

    private static void AppendHand(StringBuilder sb, string stamp, string name, bool raised)
    {
        sb.Append("**").Append(stamp).Append("** — ").Append(name)
          .AppendLine(raised ? " ✋ raised hand" : " ✋ lowered hand")
          .AppendLine();
    }

    private static void AppendReaction(StringBuilder sb, string stamp, string emoji)
    {
        sb.Append("**").Append(stamp).Append("** — reaction ").AppendLine(emoji)
          .AppendLine();
    }

    private static void AppendSlide(StringBuilder sb, string stamp, string text)
    {
        sb.Append("**").Append(stamp).AppendLine("** — slide change")
          .AppendLine("```")
          .AppendLine(text)
          .AppendLine("```")
          .AppendLine();
    }

    private static string DisplayName(string personaId, SessionManifest manifest)
    {
        foreach (var p in manifest.Roster)
        {
            if (p.Id == personaId)
            {
                return p.Name;
            }
        }
        return personaId;
    }
}
