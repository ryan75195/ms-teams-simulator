import React, { useEffect, useMemo, useRef, useState } from "react";
import { createRoot } from "react-dom/client";
import {
  Button,
  FluentProvider,
  Slider,
  Switch,
  Tooltip,
  teamsDarkTheme,
} from "@fluentui/react-components";
import {
  AddRegular,
  CameraOffRegular,
  CameraRegular,
  ChatRegular,
  DismissRegular,
  EmojiRegular,
  GridRegular,
  HandRightRegular,
  MicOffRegular,
  MicRegular,
  MoreHorizontalRegular,
  OptionsRegular,
  PeopleTeamRegular,
  SearchRegular,
  SendRegular,
  ShareScreenStartRegular,
  ShareScreenStopRegular,
} from "@fluentui/react-icons";
import "./styles.css";

const firstNames = [
  "Ryan", "Serena", "Anuj", "Ray", "Isaac", "Charlotte", "Danielle", "Krystal",
  "Bryan", "Eva", "Kayo", "Beth", "Daichi", "Kian", "Alvin", "Maya", "Theo",
  "Priya", "Marcus", "Lena", "Diego", "Aisha", "Owen", "Yuki", "Felix", "Nadia",
  "Liam", "Sofia", "Jin", "Elena", "Tariq", "Hana", "Noor", "Sebastian", "Mei",
  "Jonas", "Amara", "Hugo", "Zara", "Casey",
];

const lastNames = [
  "Khan", "Davis", "Kapoor", "Tanaka", "Summers", "de Crum", "Booker",
  "McMurray", "Wright", "Terrazas", "Miwa", "Davies", "Fukuda", "Lambert",
  "Tao", "Rodriguez", "Pereira", "Hossain", "Ng", "Andersson", "Cohen",
  "Bernal", "Wei", "Singh", "Patel", "Lindqvist", "Costa", "Adebayo", "Park",
  "Howard", "O'Connor", "Schmidt", "Hassan", "Bauer", "Vasquez", "Petrov",
  "Kim", "Larsen", "Yamamoto", "Ali",
];

const palette = [
  "#7A4F8B", "#3F628E", "#6E5BC5", "#A05E5E", "#3C7A6B", "#8A6234", "#5E7F3E",
  "#A06544", "#4F8295", "#7E4F8B", "#6B8A52", "#9A6B38", "#557A46", "#915C83",
  "#3A7CA5", "#8A3480",
];

const chatPrompts = [
  "Could you go back to the EMEA pipeline slide?",
  "The EMEA numbers look strong.",
  "Can we get the recording after this?",
  "Question for Q&A: what changed in the forecast?",
  "Audio is clear here.",
  "Can you expand on the partner motion?",
  "That customer story is useful.",
  "Will this affect the rollout timing?",
  "Sharing this with my team after.",
  "Great deck.",
  "+1 to that suggestion.",
  "Could we double-click on the EMEA pipeline?",
  "Forecast looks a bit aggressive — happy to take offline.",
  "Slide 4 still has last quarter's caption.",
];

const reactionEmoji = ["👍", "👏", "❤️", "🙂", "🎉", "😮"];

function initials(name) {
  return name
    .split(/\s+/)
    .filter(Boolean)
    .map((part) => part[0])
    .join("")
    .slice(0, 2)
    .toUpperCase();
}

function formatElapsed(seconds) {
  const h = Math.floor(seconds / 3600);
  const m = String(Math.floor((seconds % 3600) / 60)).padStart(2, "0");
  const s = String(seconds % 60).padStart(2, "0");
  return h > 0 ? `${h}:${m}:${s}` : `${m}:${s}`;
}

function formatChatTime(ts) {
  const d = new Date(ts);
  let h = d.getHours();
  const m = String(d.getMinutes()).padStart(2, "0");
  const ampm = h >= 12 ? "PM" : "AM";
  h = h % 12 || 12;
  return `${h}:${m} ${ampm}`;
}

function makeParticipant(i) {
  const isYou = i === 0;
  const firstIdx = i % firstNames.length;
  const lastIdx = Math.floor(i / firstNames.length + i * 0.37) % lastNames.length;
  const colorIdx = (i * 7 + 3) % palette.length;
  const name = isYou ? "Ryan Khan" : `${firstNames[firstIdx]} ${lastNames[lastIdx]}`;
  return {
    id: i,
    name,
    you: isYou,
    color: palette[colorIdx],
    speaking: false,
    hand: false,
    muted: !isYou && Math.random() > 0.05,
  };
}

function IconButton({ label, icon, active, onClick, badge, danger }) {
  return (
    <Tooltip content={label} relationship="label" positioning="below" withArrow={false}>
      <button
        type="button"
        className={`ribbon-btn${active ? " is-active" : ""}${danger ? " is-danger" : ""}`}
        onClick={onClick}
        aria-label={label}
        aria-pressed={active || undefined}
      >
        <span className="ribbon-icon">{icon}</span>
        {badge !== undefined && (
          <span className="ribbon-badge">{badge > 999 ? "999+" : badge}</span>
        )}
      </button>
    </Tooltip>
  );
}

function Tile({ person, reactions, small, presenter }) {
  return (
    <div
      className={
        `tile${small ? " tile--small" : ""}` +
        `${person.speaking ? " is-speaking" : ""}` +
        `${person.hand ? " is-hand" : ""}` +
        `${presenter ? " is-presenter" : ""}`
      }
    >
      <div className="tile-canvas">
        <div className="tile-avatar" style={{ background: person.color }}>
          {initials(person.name)}
        </div>
      </div>
      {person.hand && (
        <span className="tile-hand" aria-label="Hand raised">
          ✋
        </span>
      )}
      {presenter && <span className="tile-presenter-tag">Presenting</span>}
      <div className="tile-foot">
        {person.muted && (
          <span className="tile-mic" aria-label="Muted">
            <MicOffRegular />
          </span>
        )}
        <span className="tile-name">
          {person.you ? `${person.name} (You)` : person.name}
        </span>
      </div>
      <div className="tile-reactions" aria-hidden="true">
        {reactions?.map((r) => (
          <span key={r.id} className="reaction" style={{ "--x": `${r.x}%` }}>
            {r.emoji}
          </span>
        ))}
      </div>
    </div>
  );
}

function ChatPane({ chat, participants, draft, setDraft, onSend, onClose }) {
  const groups = useMemo(() => {
    const out = [];
    chat.forEach((msg) => {
      const last = out[out.length - 1];
      if (last && last.p === msg.p && msg.ts - last.lastTs < 90_000) {
        last.messages.push(msg);
        last.lastTs = msg.ts;
      } else {
        out.push({ p: msg.p, firstTs: msg.ts, lastTs: msg.ts, messages: [msg] });
      }
    });
    return out.reverse();
  }, [chat]);

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

function PersonRow({ person, highlight }) {
  return (
    <li
      className={
        `person-row${highlight ? " is-highlight" : ""}` +
        `${person.speaking ? " is-speaking" : ""}`
      }
    >
      <div className="person-avatar" style={{ background: person.color }}>
        {initials(person.name)}
      </div>
      <div className="person-info">
        <span className="person-name">
          {person.you ? `${person.name} (You)` : person.name}
        </span>
        {person.you && <span className="person-tag">Organizer</span>}
      </div>
      <div className="person-icons">
        {person.hand && (
          <span className="row-hand" aria-label="Hand raised">
            ✋
          </span>
        )}
        <span
          className={`row-mic${person.muted ? " is-muted" : " is-live"}`}
          aria-label={person.muted ? "Muted" : "Unmuted"}
        >
          {person.muted ? <MicOffRegular /> : <MicRegular />}
        </span>
      </div>
    </li>
  );
}

function PeoplePane({ participants, audienceSize, onClose }) {
  const [search, setSearch] = useState("");
  const [shownCount, setShownCount] = useState(50);

  const handsUp = useMemo(
    () => participants.filter((p) => p.hand),
    [participants]
  );
  const filtered = useMemo(() => {
    if (!search) return participants;
    const s = search.toLowerCase();
    return participants.filter((p) => p.name.toLowerCase().includes(s));
  }, [participants, search]);
  const shown = filtered.slice(0, shownCount);
  const unmuted = useMemo(
    () => participants.filter((p) => !p.muted).length,
    [participants]
  );

  return (
    <aside className="pane people-pane">
      <header className="pane-head">
        <h2>Participants</h2>
        <div className="pane-head-actions">
          <button className="pane-head-btn" aria-label="More options">
            <MoreHorizontalRegular />
          </button>
          <button
            className="pane-head-btn"
            aria-label="Close participants"
            onClick={onClose}
          >
            <DismissRegular />
          </button>
        </div>
      </header>
      <div className="pane-actions">
        <button className="primary-btn" type="button">
          <AddRegular /> Invite
        </button>
      </div>
      <label className="pane-search">
        <SearchRegular />
        <input
          placeholder="Search for participants"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
        />
      </label>
      <div className="people-scroll">
        {handsUp.length > 0 && (
          <section className="people-section">
            <header className="section-head">
              <span>Raised hands ({handsUp.length})</span>
            </header>
            <ul className="people-list">
              {handsUp.slice(0, 12).map((p) => (
                <PersonRow key={`hand-${p.id}`} person={p} highlight />
              ))}
            </ul>
          </section>
        )}
        <section className="people-section">
          <header className="section-head">
            <span>In this meeting ({audienceSize})</span>
            <span className="section-count">{unmuted} unmuted</span>
          </header>
          <ul className="people-list">
            {shown.map((p) => (
              <PersonRow key={p.id} person={p} />
            ))}
          </ul>
          {filtered.length > shownCount && (
            <button
              className="more-btn"
              type="button"
              onClick={() => setShownCount((c) => c + 100)}
            >
              Show {Math.min(100, filtered.length - shownCount)} more
            </button>
          )}
        </section>
      </div>
    </aside>
  );
}

function SimPanel(props) {
  const {
    audienceSize,
    setAudienceSize,
    engagement,
    setEngagement,
    noise,
    setNoise,
    autoChat,
    setAutoChat,
    autoReactions,
    setAutoReactions,
    speakingCount,
    handsCount,
    onApplause,
    onQA,
    onClose,
  } = props;

  return (
    <aside className="sim-panel" aria-label="Simulator controls">
      <header className="sim-head">
        <span>SIMULATOR</span>
        <button
          className="pane-head-btn"
          aria-label="Close simulator"
          onClick={onClose}
        >
          <DismissRegular />
        </button>
      </header>
      <div className="sim-body">
        <div className="sim-stat-row">
          <div className="sim-stat">
            <strong>{audienceSize}</strong>
            <span>Attendees</span>
          </div>
          <div className="sim-stat">
            <strong>{speakingCount}</strong>
            <span>Speaking</span>
          </div>
          <div className="sim-stat">
            <strong>{handsCount}</strong>
            <span>Hands</span>
          </div>
        </div>
        <label className="sim-field">
          <span>
            Audience size <em>{audienceSize}</em>
          </span>
          <Slider
            min={9}
            max={1000}
            step={1}
            value={audienceSize}
            onChange={(_, d) => setAudienceSize(d.value)}
          />
        </label>
        <label className="sim-field">
          <span>
            Engagement <em>{engagement}/10</em>
          </span>
          <Slider
            min={1}
            max={10}
            step={1}
            value={engagement}
            onChange={(_, d) => setEngagement(d.value)}
          />
        </label>
        <label className="sim-field">
          <span>
            Background noise <em>{noise}/10</em>
          </span>
          <Slider
            min={0}
            max={10}
            step={1}
            value={noise}
            onChange={(_, d) => setNoise(d.value)}
          />
        </label>
        <div className="sim-toggles">
          <Switch
            checked={autoChat}
            onChange={(_, d) => setAutoChat(d.checked)}
            label="Auto chat"
          />
          <Switch
            checked={autoReactions}
            onChange={(_, d) => setAutoReactions(d.checked)}
            label="Auto reactions"
          />
        </div>
        <div className="sim-actions">
          <Button onClick={onApplause}>Applause surge</Button>
          <Button onClick={onQA}>Q&amp;A rush</Button>
        </div>
      </div>
    </aside>
  );
}

function ContentSlide({ presenterName }) {
  return (
    <div className="content-slide">
      <header className="slide-header">
        <span className="slide-tag">Sales Report — Q2 Review</span>
        <span className="slide-page">3 / 18</span>
      </header>
      <h2>EMEA pipeline outlook</h2>
      <div className="slide-grid">
        <div className="slide-stat">
          <span className="stat-label">QoQ pipeline</span>
          <strong>+18.4%</strong>
          <span className="stat-detail">vs. +12% last quarter</span>
        </div>
        <div className="slide-stat">
          <span className="stat-label">Win rate</span>
          <strong>34%</strong>
          <span className="stat-detail">+2.1pp WoW</span>
        </div>
        <div className="slide-stat">
          <span className="stat-label">Avg. deal size</span>
          <strong>£48k</strong>
          <span className="stat-detail">+£3k MoM</span>
        </div>
      </div>
      <ul className="slide-bullets">
        <li>
          Mid-market leading EMEA growth at <strong>+24%</strong>, two large logos at LOI
        </li>
        <li>
          APAC steady; pipeline coverage at <strong>3.2×</strong> for Q3
        </li>
        <li>NAMER softness on mid-market — investigating with field team</li>
      </ul>
      <footer className="slide-presenter">
        <span className="presenter-dot" />
        <span>{presenterName} is presenting</span>
        <span className="slide-confidential">Confidential — internal only</span>
      </footer>
    </div>
  );
}

function App() {
  const [audienceSize, setAudienceSize] = useState(250);
  const [engagement, setEngagement] = useState(5);
  const [noise, setNoise] = useState(2);
  const [elapsed, setElapsed] = useState(2247);
  const [participants, setParticipants] = useState(() =>
    Array.from({ length: 250 }, (_, i) => makeParticipant(i))
  );
  const [chat, setChat] = useState(() => {
    const now = Date.now();
    return [
      { id: 1, p: 7, text: "Joining a few minutes late, sorry!", ts: now - 240_000 },
      { id: 2, p: 3, text: "No problem — we just started the agenda.", ts: now - 200_000 },
      { id: 3, p: 14, text: "Slides are visible.", ts: now - 160_000 },
      { id: 4, p: 8, text: "Could you go back to the EMEA pipeline slide?", ts: now - 90_000 },
      { id: 5, p: 0, text: "Sure — one moment.", ts: now - 80_000 },
      { id: 6, p: 22, text: "The EMEA numbers look strong.", ts: now - 40_000 },
      { id: 7, p: 5, text: "+1 to that.", ts: now - 30_000 },
    ];
  });
  const [chatDraft, setChatDraft] = useState("");
  const [reactionFloats, setReactionFloats] = useState([]);
  const [rightPane, setRightPane] = useState("chat");
  const [debugOpen, setDebugOpen] = useState(false);
  const [autoChat, setAutoChat] = useState(true);
  const [autoReactions, setAutoReactions] = useState(true);
  const [cameraOff, setCameraOff] = useState(true);
  const [muted, setMuted] = useState(true);
  const [sharing, setSharing] = useState(true);
  const [handRaised, setHandRaised] = useState(false);

  const chatIdRef = useRef(100);
  const reactionIdRef = useRef(0);

  // Resize participant list when audienceSize changes; preserve existing state.
  useEffect(() => {
    setParticipants((prev) => {
      if (audienceSize === prev.length) return prev;
      if (audienceSize > prev.length) {
        const add = Array.from(
          { length: audienceSize - prev.length },
          (_, i) => makeParticipant(prev.length + i)
        );
        return [...prev, ...add];
      }
      return prev.slice(0, audienceSize);
    });
  }, [audienceSize]);

  // Tick the meeting clock.
  useEffect(() => {
    const t = window.setInterval(() => setElapsed((e) => e + 1), 1000);
    return () => window.clearInterval(t);
  }, []);

  // Mirror user-controlled flags onto participants[0].
  useEffect(() => {
    setParticipants((prev) => {
      if (!prev[0] || (prev[0].hand === handRaised && prev[0].muted === muted)) {
        return prev;
      }
      const next = prev.slice();
      next[0] = { ...next[0], hand: handRaised, muted };
      return next;
    });
  }, [handRaised, muted]);

  // Audience activity loop: speakers + hand raises.
  useEffect(() => {
    const t = window.setInterval(() => {
      setParticipants((prev) => {
        const speakChance = 0.02 + noise * 0.005;
        const handChance = 0.002 + engagement * 0.0014;
        const handDropChance = 0.12;
        return prev.map((p, i) => {
          const isPresenter = !p.you && i === 1;
          const speaking = isPresenter
            ? Math.random() < 0.92
            : !p.muted && Math.random() < speakChance;
          let hand = p.hand;
          if (!p.you) {
            if (p.hand && Math.random() < handDropChance) hand = false;
            else if (!p.hand && Math.random() < handChance) hand = true;
          }
          return { ...p, speaking, hand };
        });
      });
    }, 1500);
    return () => window.clearInterval(t);
  }, [engagement, noise]);

  // Auto chat trickle.
  useEffect(() => {
    if (!autoChat) return undefined;
    const t = window.setInterval(() => {
      if (Math.random() >= engagement / 12) return;
      const pIndex = Math.floor(Math.random() * Math.max(1, audienceSize));
      const text = chatPrompts[Math.floor(Math.random() * chatPrompts.length)];
      setChat((prev) => {
        const id = ++chatIdRef.current;
        return [...prev, { id, p: pIndex, text, ts: Date.now() }].slice(-80);
      });
    }, 2400);
    return () => window.clearInterval(t);
  }, [autoChat, engagement, audienceSize]);

  // Auto reactions float.
  useEffect(() => {
    if (!autoReactions) return undefined;
    const t = window.setInterval(() => {
      if (Math.random() >= engagement / 8) return;
      addReaction();
    }, 900);
    return () => window.clearInterval(t);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [autoReactions, engagement, sharing]);

  function addReaction(emoji, tileIndex) {
    const tileCount = sharing ? 4 : 9;
    const e = emoji || reactionEmoji[Math.floor(Math.random() * reactionEmoji.length)];
    const tile =
      tileIndex !== undefined ? tileIndex : Math.floor(Math.random() * tileCount);
    const x = 25 + Math.random() * 50;
    const id = ++reactionIdRef.current;
    setReactionFloats((prev) => [...prev, { id, emoji: e, tile, x }]);
    window.setTimeout(() => {
      setReactionFloats((prev) => prev.filter((r) => r.id !== id));
    }, 1900);
  }

  function applauseSurge() {
    const tileCount = sharing ? 4 : 9;
    for (let i = 0; i < 24; i += 1) {
      window.setTimeout(() => {
        addReaction(
          i % 3 === 0 ? "🎉" : "👏",
          Math.floor(Math.random() * tileCount)
        );
      }, i * 80);
    }
  }

  function questionRush() {
    const questions = [
      "How does this affect the launch date?",
      "Can finance review the budget slide?",
      "What changed in the sales assumption?",
      "Is there a customer pilot group?",
      "Can we get the risk register after this?",
      "Will this be reflected in next week's forecast?",
    ];
    questions.forEach((text, idx) => {
      window.setTimeout(() => {
        const pIdx = (idx + 8) % Math.max(1, audienceSize);
        setChat((prev) => {
          const id = ++chatIdRef.current;
          return [
            ...prev,
            { id, p: pIdx, text, ts: Date.now() + idx * 100 },
          ].slice(-80);
        });
      }, idx * 250);
    });
    setParticipants((prev) =>
      prev.map((p, i) =>
        i > 0 && i < 25 ? { ...p, hand: Math.random() > 0.4 ? true : p.hand } : p
      )
    );
  }

  function sendChat() {
    const text = chatDraft.trim();
    if (!text) return;
    setChat((prev) => {
      const id = ++chatIdRef.current;
      return [...prev, { id, p: 0, text, ts: Date.now() }].slice(-80);
    });
    setChatDraft("");
  }

  const galleryCount = 9;
  const gallery = useMemo(
    () => participants.slice(0, galleryCount),
    [participants]
  );
  const filmstrip = useMemo(
    () => participants.slice(0, 4),
    [participants]
  );

  const reactionsByTile = useMemo(() => {
    const map = new Map();
    reactionFloats.forEach((r) => {
      const list = map.get(r.tile) || [];
      list.push(r);
      map.set(r.tile, list);
    });
    return map;
  }, [reactionFloats]);

  const speakingCount = useMemo(
    () => participants.filter((p) => p.speaking).length,
    [participants]
  );
  const handsCount = useMemo(
    () => participants.filter((p) => p.hand).length,
    [participants]
  );

  const presenter = participants[1];

  return (
    <FluentProvider
      theme={teamsDarkTheme}
      style={{
        background: "transparent",
        minHeight: "100vh",
        display: "grid",
        placeItems: "center",
        padding: 0,
      }}
    >
      <div className="teams-app">
        <header className="ribbon">
          <div className="ribbon-left">
            <div className="meeting-id">
              <span className="ribbon-app-icon" aria-hidden="true">
                <svg width="22" height="22" viewBox="0 0 22 22" fill="none">
                  <rect x="2.5" y="5" width="11" height="12" rx="2.5" fill="#5B5FC7" />
                  <rect x="2.5" y="5" width="11" height="3.4" fill="#7B7FE0" />
                  <text
                    x="8"
                    y="14.6"
                    textAnchor="middle"
                    fontFamily="Segoe UI, sans-serif"
                    fontSize="7"
                    fontWeight="700"
                    fill="white"
                  >
                    T
                  </text>
                  <path d="M14.4 8.5L19.5 6.2V15.8L14.4 13.5V8.5Z" fill="#5B5FC7" />
                </svg>
              </span>
              <div className="meeting-id-text">
                <strong>Sales Report — Q2 Review</strong>
                <span>{audienceSize} participants</span>
              </div>
            </div>
            <span className="rec-pill" aria-label="Recording in progress">
              <span className="rec-dot" aria-hidden="true" /> Rec
            </span>
            <span className="elapsed-clock" aria-label="Elapsed time">
              {formatElapsed(elapsed)}
            </span>
          </div>
          <nav className="ribbon-cluster" aria-label="Meeting controls">
            <IconButton
              label="People"
              icon={<PeopleTeamRegular />}
              active={rightPane === "people"}
              onClick={() =>
                setRightPane((rp) => (rp === "people" ? null : "people"))
              }
              badge={audienceSize}
            />
            <IconButton
              label="Chat"
              icon={<ChatRegular />}
              active={rightPane === "chat"}
              onClick={() =>
                setRightPane((rp) => (rp === "chat" ? null : "chat"))
              }
            />
            <IconButton
              label="React"
              icon={<EmojiRegular />}
              onClick={() => addReaction()}
            />
            <IconButton
              label={handRaised ? "Lower hand" : "Raise hand"}
              icon={<HandRightRegular />}
              active={handRaised}
              onClick={() => setHandRaised((h) => !h)}
            />
            <IconButton label="View" icon={<GridRegular />} />
            <IconButton label="More" icon={<MoreHorizontalRegular />} />
            <span className="ribbon-divider" aria-hidden="true" />
            <IconButton
              label={cameraOff ? "Turn camera on" : "Turn camera off"}
              icon={cameraOff ? <CameraOffRegular /> : <CameraRegular />}
              active={!cameraOff}
              danger={cameraOff}
              onClick={() => setCameraOff((o) => !o)}
            />
            <IconButton
              label={muted ? "Unmute" : "Mute"}
              icon={muted ? <MicOffRegular /> : <MicRegular />}
              active={!muted}
              danger={muted}
              onClick={() => setMuted((o) => !o)}
            />
            <IconButton
              label={sharing ? "Stop sharing" : "Share screen"}
              icon={sharing ? <ShareScreenStopRegular /> : <ShareScreenStartRegular />}
              active={sharing}
              onClick={() => setSharing((o) => !o)}
            />
            <button className="leave-btn" type="button">
              Leave
            </button>
          </nav>
        </header>

        <div
          className={
            `body${rightPane ? " body--with-pane" : ""}` +
            `${sharing ? " body--shared" : ""}`
          }
        >
          <main className="stage">
            {sharing ? (
              <div className="content-stage">
                <ContentSlide presenterName={presenter?.name || "Presenter"} />
                <aside className="filmstrip" aria-label="Other participants">
                  {filmstrip.map((person, i) => (
                    <Tile
                      key={person.id}
                      person={person}
                      reactions={reactionsByTile.get(i)}
                      presenter={i === 1}
                      small
                    />
                  ))}
                  {audienceSize > 4 && (
                    <div className="filmstrip-more">
                      <strong>+{audienceSize - 4}</strong>
                      <span>more</span>
                    </div>
                  )}
                </aside>
              </div>
            ) : (
              <div className="gallery">
                {gallery.map((person, i) => (
                  <Tile
                    key={person.id}
                    person={person}
                    reactions={reactionsByTile.get(i)}
                  />
                ))}
              </div>
            )}
          </main>

          {rightPane === "chat" && (
            <ChatPane
              chat={chat}
              participants={participants}
              draft={chatDraft}
              setDraft={setChatDraft}
              onSend={sendChat}
              onClose={() => setRightPane(null)}
            />
          )}
          {rightPane === "people" && (
            <PeoplePane
              participants={participants}
              audienceSize={audienceSize}
              onClose={() => setRightPane(null)}
            />
          )}
        </div>

        <button
          className={`sim-toggle${debugOpen ? " is-open" : ""}`}
          type="button"
          onClick={() => setDebugOpen((o) => !o)}
          aria-pressed={debugOpen}
        >
          <OptionsRegular />
          <span>Sim</span>
        </button>

        {debugOpen && (
          <SimPanel
            audienceSize={audienceSize}
            setAudienceSize={setAudienceSize}
            engagement={engagement}
            setEngagement={setEngagement}
            noise={noise}
            setNoise={setNoise}
            autoChat={autoChat}
            setAutoChat={setAutoChat}
            autoReactions={autoReactions}
            setAutoReactions={setAutoReactions}
            speakingCount={speakingCount}
            handsCount={handsCount}
            onApplause={applauseSurge}
            onQA={questionRush}
            onClose={() => setDebugOpen(false)}
          />
        )}
      </div>
    </FluentProvider>
  );
}

createRoot(document.getElementById("root")).render(<App />);
