import { useMemo } from "react";
import {
  DismissRegular,
  MoreHorizontalRegular,
  SendRegular,
} from "@fluentui/react-icons";
import { CHAT_GROUPING_WINDOW_MS } from "../constants";
import { formatChatTime, initials } from "../helpers";

function groupMessages(chat) {
  const groups = [];
  chat.forEach((msg) => {
    const last = groups[groups.length - 1];
    if (last && last.p === msg.p && msg.ts - last.lastTs < CHAT_GROUPING_WINDOW_MS) {
      last.messages.push(msg);
      last.lastTs = msg.ts;
    } else {
      groups.push({ p: msg.p, firstTs: msg.ts, lastTs: msg.ts, messages: [msg] });
    }
  });
  return groups.reverse();
}

export function ChatPane({ chat, participants, draft, setDraft, onSend, onClose }) {
  const groups = useMemo(() => groupMessages(chat), [chat]);

  return (
    <aside className="pane chat-pane">
      <header className="pane-head">
        <h2>Chat</h2>
        <div className="pane-head-actions">
          <button className="pane-head-btn" aria-label="More chat options">
            <MoreHorizontalRegular />
          </button>
          <button className="pane-head-btn" aria-label="Close chat" onClick={onClose}>
            <DismissRegular />
          </button>
        </div>
      </header>
      <div className="pane-banner">In-meeting chat — visible to everyone</div>
      <div className="thread">
        {groups.map((group, gi) => {
          const person =
            participants.find((p) => p.id === group.p) || participants[0];
          return (
            <article className="msg-group" key={`${gi}-${group.firstTs}`}>
              <div
                className="msg-avatar"
                style={{ background: person.color }}
                aria-hidden="true"
              >
                {initials(person.name)}
              </div>
              <div className="msg-body">
                <header className="msg-meta">
                  <strong>{person.you ? "You" : person.name}</strong>
                  <span>{formatChatTime(group.firstTs)}</span>
                </header>
                {group.messages.map((m) => (
                  <p className="msg-text" key={m.id}>
                    {m.text}
                  </p>
                ))}
              </div>
            </article>
          );
        })}
      </div>
      <form
        className="composer"
        onSubmit={(e) => {
          e.preventDefault();
          onSend();
        }}
      >
        <div className="composer-input">
          <input
            placeholder="Type a message"
            value={draft}
            onChange={(e) => setDraft(e.target.value)}
            aria-label="Message"
          />
        </div>
        <button
          type="submit"
          className="composer-send"
          aria-label="Send"
          disabled={!draft.trim()}
        >
          <SendRegular />
        </button>
      </form>
    </aside>
  );
}
