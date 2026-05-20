import { useEffect, useMemo, useRef, useState } from "react";
import { createRoot } from "react-dom/client";
import { FluentProvider, teamsDarkTheme } from "@fluentui/react-components";
import { OptionsRegular } from "@fluentui/react-icons";
import {
  ACTIVITY_TICK_MS,
  APPLAUSE_BURST_COUNT,
  APPLAUSE_STEP_MS,
  CHAT_TICK_MS,
  FILMSTRIP_SIZE,
  GALLERY_SIZE,
  INITIAL_AUDIENCE,
  INITIAL_ELAPSED_SECONDS,
  MAX_CHAT_HISTORY,
  PRESENTER_INDEX,
  REACTION_LIFETIME_MS,
  REACTION_TICK_MS,
  YOU_INDEX,
  chatPrompts,
  qaRushQuestions,
  reactionEmoji,
} from "./constants";
import { makeParticipant } from "./helpers";
import { ChatPane } from "./components/ChatPane";
import { ContentStage } from "./components/ContentStage";
import { Gallery } from "./components/Gallery";
import { PeoplePane } from "./components/PeoplePane";
import { Ribbon } from "./components/Ribbon";
import { SimPanel } from "./components/SimPanel";
import "./styles.css";

function buildSeedChat() {
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
}

function App() {
  const [audienceSize, setAudienceSize] = useState(INITIAL_AUDIENCE);
  const [engagement, setEngagement] = useState(5);
  const [noise, setNoise] = useState(2);
  const [elapsed, setElapsed] = useState(INITIAL_ELAPSED_SECONDS);
  const [participants, setParticipants] = useState(() =>
    Array.from({ length: INITIAL_AUDIENCE }, (_, i) => makeParticipant(i))
  );
  const [chat, setChat] = useState(buildSeedChat);
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

  // Mirror user-controlled flags onto participants[YOU_INDEX].
  useEffect(() => {
    setParticipants((prev) => {
      const me = prev[YOU_INDEX];
      if (!me || (me.hand === handRaised && me.muted === muted)) return prev;
      const next = prev.slice();
      next[YOU_INDEX] = { ...me, hand: handRaised, muted };
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
          const isPresenter = !p.you && i === PRESENTER_INDEX;
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
    }, ACTIVITY_TICK_MS);
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
        return [...prev, { id, p: pIndex, text, ts: Date.now() }].slice(
          -MAX_CHAT_HISTORY
        );
      });
    }, CHAT_TICK_MS);
    return () => window.clearInterval(t);
  }, [autoChat, engagement, audienceSize]);

  // Auto reactions float.
  useEffect(() => {
    if (!autoReactions) return undefined;
    const tileCount = sharing ? FILMSTRIP_SIZE : GALLERY_SIZE;
    const t = window.setInterval(() => {
      if (Math.random() >= engagement / 8) return;
      spawnReaction(tileCount);
    }, REACTION_TICK_MS);
    return () => window.clearInterval(t);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [autoReactions, engagement, sharing]);

  function spawnReaction(tileCount, emoji, tileIndex) {
    const e =
      emoji || reactionEmoji[Math.floor(Math.random() * reactionEmoji.length)];
    const tile =
      tileIndex !== undefined ? tileIndex : Math.floor(Math.random() * tileCount);
    const x = 25 + Math.random() * 50;
    const id = ++reactionIdRef.current;
    setReactionFloats((prev) => [...prev, { id, emoji: e, tile, x }]);
    window.setTimeout(() => {
      setReactionFloats((prev) => prev.filter((r) => r.id !== id));
    }, REACTION_LIFETIME_MS);
  }

  function react() {
    spawnReaction(sharing ? FILMSTRIP_SIZE : GALLERY_SIZE);
  }

  function applauseSurge() {
    const tileCount = sharing ? FILMSTRIP_SIZE : GALLERY_SIZE;
    for (let i = 0; i < APPLAUSE_BURST_COUNT; i += 1) {
      window.setTimeout(() => {
        spawnReaction(
          tileCount,
          i % 3 === 0 ? "🎉" : "👏",
          Math.floor(Math.random() * tileCount)
        );
      }, i * APPLAUSE_STEP_MS);
    }
  }

  function questionRush() {
    qaRushQuestions.forEach((text, idx) => {
      window.setTimeout(() => {
        const pIdx = (idx + 8) % Math.max(1, audienceSize);
        setChat((prev) => {
          const id = ++chatIdRef.current;
          return [
            ...prev,
            { id, p: pIdx, text, ts: Date.now() + idx * 100 },
          ].slice(-MAX_CHAT_HISTORY);
        });
      }, idx * 250);
    });
    setParticipants((prev) =>
      prev.map((p, i) =>
        i > 0 && i < 25
          ? { ...p, hand: Math.random() > 0.4 ? true : p.hand }
          : p
      )
    );
  }

  function sendChat() {
    const text = chatDraft.trim();
    if (!text) return;
    setChat((prev) => {
      const id = ++chatIdRef.current;
      return [...prev, { id, p: YOU_INDEX, text, ts: Date.now() }].slice(
        -MAX_CHAT_HISTORY
      );
    });
    setChatDraft("");
  }

  function togglePane(target) {
    setRightPane((rp) => (rp === target ? null : target));
  }

  const gallery = useMemo(
    () => participants.slice(0, GALLERY_SIZE),
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

  return (
    <FluentProvider theme={teamsDarkTheme} className="app-root">
      <div className="teams-app">
        <Ribbon
          audienceSize={audienceSize}
          elapsed={elapsed}
          rightPane={rightPane}
          togglePane={togglePane}
          onReact={react}
          handRaised={handRaised}
          toggleHand={() => setHandRaised((h) => !h)}
          cameraOff={cameraOff}
          toggleCamera={() => setCameraOff((o) => !o)}
          muted={muted}
          toggleMuted={() => setMuted((o) => !o)}
          sharing={sharing}
          toggleSharing={() => setSharing((o) => !o)}
        />

        <div
          className={
            `body${rightPane ? " body--with-pane" : ""}` +
            `${sharing ? " body--shared" : ""}`
          }
        >
          <main className="stage">
            {sharing ? (
              <ContentStage
                participants={participants}
                audienceSize={audienceSize}
                reactionsByTile={reactionsByTile}
              />
            ) : (
              <Gallery participants={gallery} reactionsByTile={reactionsByTile} />
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
