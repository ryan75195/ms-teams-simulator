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
import { useSession } from "./useSession";
import { useLiveMic } from "./audio/useLiveMic";
import { ChatPane } from "./components/ChatPane";
import { ContentStage } from "./components/ContentStage";
import { Gallery } from "./components/Gallery";
import { PeoplePane } from "./components/PeoplePane";
import { Ribbon } from "./components/Ribbon";
import { SimPanel } from "./components/SimPanel";
import { TranscriptPane } from "./components/TranscriptPane";
import "./styles.css";

const API_URL = import.meta.env.VITE_API_URL || null;
const WS_BASE_URL = API_URL ? API_URL.replace(/^http/, "ws") : null;
const PARTIAL_TRANSCRIPT_CLEAR_MS = 2000;
const MIC_ERROR_DISPLAY_MS = 6000;
const YOU_PERSONA_ID = "you";

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
  const apiMode = !!API_URL;
  const api = useSession(API_URL, { audienceSize: INITIAL_AUDIENCE });

  const [audienceSize, setAudienceSize] = useState(INITIAL_AUDIENCE);
  const [engagement, setEngagement] = useState(5);
  const [noise, setNoise] = useState(2);
  const [elapsed, setElapsed] = useState(INITIAL_ELAPSED_SECONDS);
  const [localParticipants, setLocalParticipants] = useState(() =>
    Array.from({ length: INITIAL_AUDIENCE }, (_, i) => makeParticipant(i))
  );
  const [localChat, setLocalChat] = useState(buildSeedChat);
  const [chatDraft, setChatDraft] = useState("");
  const [reactionFloats, setReactionFloats] = useState([]);
  const [rightPane, setRightPane] = useState("chat");
  const [debugOpen, setDebugOpen] = useState(false);
  const [autoChat, setAutoChat] = useState(true);
  const [autoReactions, setAutoReactions] = useState(true);
  const [cameraOff, setCameraOff] = useState(true);
  const [muted, setMuted] = useState(true);
  const [sharing, setSharing] = useState(true);
  const [localHandRaised, setLocalHandRaised] = useState(false);

  const chatIdRef = useRef(100);
  const reactionIdRef = useRef(0);
  const partialClearTimerRef = useRef(null);
  const slideDebounceRef = useRef(null);
  const micErrorTimerRef = useRef(null);
  const [partialTranscript, setPartialTranscript] = useState("");
  const [slideDraft, setSlideDraft] = useState("");
  const [micError, setMicError] = useState(null);

  useEffect(() => {
    if (apiMode) return;
    setLocalParticipants((prev) => {
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
  }, [audienceSize, apiMode]);

  useEffect(() => {
    const t = window.setInterval(() => setElapsed((e) => e + 1), 1000);
    return () => window.clearInterval(t);
  }, []);

  useEffect(() => {
    if (typeof window !== "undefined" && window.electronApp?.isElectron) {
      document.documentElement.classList.add("is-electron");
    }
  }, []);

  useEffect(() => {
    function onKey(e) {
      const k = e.key.toLowerCase();
      if (e.ctrlKey && e.shiftKey) {
        if (k === "m") {
          e.preventDefault();
          setMuted((v) => !v);
        } else if (k === "o") {
          e.preventDefault();
          setCameraOff((v) => !v);
        } else if (k === "k") {
          e.preventDefault();
          toggleHand();
        }
      } else if (e.ctrlKey && !e.shiftKey && k === "e") {
        const target = e.target;
        const tag = target?.tagName;
        if (tag === "INPUT" || tag === "TEXTAREA") return;
        e.preventDefault();
        setRightPane((rp) => (rp === "chat" ? null : "chat"));
      }
    }
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  });

  useEffect(() => {
    if (apiMode) return;
    setLocalParticipants((prev) => {
      const me = prev[YOU_INDEX];
      if (!me || (me.hand === localHandRaised && me.muted === muted)) return prev;
      const next = prev.slice();
      next[YOU_INDEX] = { ...me, hand: localHandRaised, muted };
      return next;
    });
  }, [localHandRaised, muted, apiMode]);

  useEffect(() => {
    if (apiMode) return undefined;
    const t = window.setInterval(() => {
      setLocalParticipants((prev) => {
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
  }, [engagement, noise, apiMode]);

  useEffect(() => {
    if (apiMode || !autoChat) return undefined;
    const t = window.setInterval(() => {
      if (Math.random() >= engagement / 12) return;
      const pIndex = Math.floor(Math.random() * Math.max(1, audienceSize));
      const text = chatPrompts[Math.floor(Math.random() * chatPrompts.length)];
      setLocalChat((prev) => {
        const id = ++chatIdRef.current;
        return [...prev, { id, p: pIndex, text, ts: Date.now() }].slice(
          -MAX_CHAT_HISTORY
        );
      });
    }, CHAT_TICK_MS);
    return () => window.clearInterval(t);
  }, [autoChat, engagement, audienceSize, apiMode]);

  useEffect(() => {
    if (apiMode || !autoReactions) return undefined;
    const tileCount = sharing ? FILMSTRIP_SIZE : GALLERY_SIZE;
    const t = window.setInterval(() => {
      if (Math.random() >= engagement / 8) return;
      spawnLocalReaction(tileCount);
    }, REACTION_TICK_MS);
    return () => window.clearInterval(t);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [autoReactions, engagement, sharing, apiMode]);

  function spawnLocalReaction(tileCount, emoji, tileIndex) {
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
    const tileCount = sharing ? FILMSTRIP_SIZE : GALLERY_SIZE;
    if (apiMode) {
      api.send.event({
        kind: "reaction",
        tile: Math.floor(Math.random() * tileCount),
        emoji:
          reactionEmoji[Math.floor(Math.random() * reactionEmoji.length)],
      });
      return;
    }
    spawnLocalReaction(tileCount);
  }

  function applauseSurge() {
    const tileCount = sharing ? FILMSTRIP_SIZE : GALLERY_SIZE;
    for (let i = 0; i < APPLAUSE_BURST_COUNT; i += 1) {
      const emoji = i % 3 === 0 ? "🎉" : "👏";
      const tile = Math.floor(Math.random() * tileCount);
      window.setTimeout(() => {
        if (apiMode) {
          api.send.event({ kind: "reaction", tile, emoji });
        } else {
          spawnLocalReaction(tileCount, emoji, tile);
        }
      }, i * APPLAUSE_STEP_MS);
    }
  }

  function questionRush() {
    if (apiMode) {
      const speakers =
        api.personas?.roster?.filter((p) => p.id !== YOU_PERSONA_ID) ?? [];
      if (speakers.length === 0) return;
      qaRushQuestions.forEach((text, idx) => {
        const persona = speakers[idx % speakers.length];
        window.setTimeout(() => {
          api.send.event({
            kind: "chat",
            personaId: persona.id,
            text,
          });
        }, idx * 250);
      });
      qaRushQuestions.slice(0, 3).forEach((_, idx) => {
        const persona = speakers[(idx + 1) % speakers.length];
        window.setTimeout(() => {
          api.send.event({
            kind: "hand-raise",
            personaId: persona.id,
            raised: true,
          });
        }, idx * 350);
      });
      return;
    }
    qaRushQuestions.forEach((text, idx) => {
      window.setTimeout(() => {
        const pIdx = (idx + 8) % Math.max(1, audienceSize);
        setLocalChat((prev) => {
          const id = ++chatIdRef.current;
          return [
            ...prev,
            { id, p: pIdx, text, ts: Date.now() + idx * 100 },
          ].slice(-MAX_CHAT_HISTORY);
        });
      }, idx * 250);
    });
    setLocalParticipants((prev) =>
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
    if (apiMode) {
      api.send.event({ kind: "chat", personaId: YOU_PERSONA_ID, text });
    } else {
      setLocalChat((prev) => {
        const id = ++chatIdRef.current;
        return [...prev, { id, p: YOU_INDEX, text, ts: Date.now() }].slice(
          -MAX_CHAT_HISTORY
        );
      });
    }
    setChatDraft("");
  }

  const youHandRaised = apiMode
    ? !!api.view.hands[YOU_PERSONA_ID]
    : localHandRaised;

  function toggleHand() {
    if (apiMode) {
      api.send.event({
        kind: "hand-raise",
        personaId: YOU_PERSONA_ID,
        raised: !youHandRaised,
      });
    } else {
      setLocalHandRaised((v) => !v);
    }
  }

  function togglePane(target) {
    setRightPane((rp) => (rp === target ? null : target));
  }

  function handlePartialTranscript(text) {
    setPartialTranscript(text);
    if (partialClearTimerRef.current) {
      window.clearTimeout(partialClearTimerRef.current);
    }
    partialClearTimerRef.current = window.setTimeout(
      () => setPartialTranscript(""),
      PARTIAL_TRANSCRIPT_CLEAR_MS
    );
  }

  useEffect(
    () => () => {
      if (partialClearTimerRef.current) {
        window.clearTimeout(partialClearTimerRef.current);
      }
      if (slideDebounceRef.current) {
        window.clearTimeout(slideDebounceRef.current);
      }
      if (micErrorTimerRef.current) {
        window.clearTimeout(micErrorTimerRef.current);
      }
    },
    []
  );

  function reportMicError(message) {
    setMicError(message);
    if (micErrorTimerRef.current) window.clearTimeout(micErrorTimerRef.current);
    micErrorTimerRef.current = window.setTimeout(
      () => setMicError(null),
      MIC_ERROR_DISPLAY_MS
    );
  }

  async function handleLeave() {
    const ok = window.confirm(
      "End this session?\n\nThe transcript and audio are saved on disk and you can review them later. A fresh session will start."
    );
    if (!ok) return;
    if (apiMode && api.endSession) {
      setMuted(true);
      setSlideDraft("");
      lastSentSlideRef.current = "";
      setPartialTranscript("");
      await api.endSession();
    }
  }

  const lastSentSlideRef = useRef("");
  function handleSlideChange(text) {
    setSlideDraft(text);
    if (!apiMode) return;
    if (slideDebounceRef.current) {
      window.clearTimeout(slideDebounceRef.current);
    }
    slideDebounceRef.current = window.setTimeout(() => {
      if (text === lastSentSlideRef.current) return;
      lastSentSlideRef.current = text;
      api.send.event({ kind: "slide-update", text });
    }, 600);
  }

  const realtimeWsUrl =
    apiMode && api.sessionId && WS_BASE_URL
      ? `${WS_BASE_URL}/sessions/${api.sessionId}/realtime`
      : null;

  const {
    state: micState,
    start: startMic,
    stop: stopMic,
  } = useLiveMic({
    wsUrl: realtimeWsUrl,
    onPartial: handlePartialTranscript,
    onError: reportMicError,
  });

  useEffect(() => {
    if (!apiMode || !realtimeWsUrl) return;
    if (!muted && micState === "idle") {
      startMic();
    } else if (muted && micState !== "idle") {
      stopMic();
    }
  }, [muted, apiMode, realtimeWsUrl, micState, startMic, stopMic]);

  useEffect(() => {
    if (micState === "idle" && !muted) {
      setMuted(true);
    }
  }, [micState, muted]);

  const apiParticipants = useMemo(() => {
    if (!apiMode || !api.personas) return null;
    return api.personas.roster.map((p) => ({
      id: p.id,
      name: p.name,
      color: p.color,
      you: p.id === YOU_PERSONA_ID,
      speaking: !!api.view.speaking[p.id],
      hand: !!api.view.hands[p.id],
      muted: p.id === YOU_PERSONA_ID ? muted : false,
    }));
  }, [apiMode, api.personas, api.view.speaking, api.view.hands, muted]);

  const participants = apiParticipants ?? localParticipants;

  const gallery = useMemo(
    () => participants.slice(0, GALLERY_SIZE),
    [participants]
  );

  const chat = useMemo(() => {
    if (!apiMode) return localChat;
    return api.view.chat.map((c) => ({
      id: c.id,
      p: c.personaId,
      text: c.text,
      ts: c.ts ? new Date(c.ts).getTime() : Date.now(),
    }));
  }, [apiMode, api.view.chat, localChat]);

  const reactionsByTile = useMemo(() => {
    const source = apiMode ? api.view.reactions : reactionFloats;
    const map = new Map();
    source.forEach((r) => {
      const list = map.get(r.tile) || [];
      list.push(r);
      map.set(r.tile, list);
    });
    return map;
  }, [apiMode, api.view.reactions, reactionFloats]);

  const speakingCount = useMemo(() => {
    if (apiMode) return Object.keys(api.view.speaking).length;
    return participants.filter((p) => p.speaking).length;
  }, [apiMode, api.view.speaking, participants]);

  const handsCount = useMemo(() => {
    if (apiMode) return Object.keys(api.view.hands).length;
    return participants.filter((p) => p.hand).length;
  }, [apiMode, api.view.hands, participants]);

  return (
    <FluentProvider theme={teamsDarkTheme} className="app-root">
      <div className="teams-app">
        <Ribbon
          audienceSize={audienceSize}
          elapsed={elapsed}
          rightPane={rightPane}
          togglePane={togglePane}
          onReact={react}
          handRaised={youHandRaised}
          toggleHand={toggleHand}
          cameraOff={cameraOff}
          toggleCamera={() => setCameraOff((o) => !o)}
          muted={muted}
          toggleMuted={() => setMuted((o) => !o)}
          sharing={sharing}
          toggleSharing={() => setSharing((o) => !o)}
          onLeave={handleLeave}
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
          {rightPane === "transcript" && (
            <TranscriptPane
              transcripts={apiMode ? api.view.transcripts : []}
              partial={partialTranscript}
              onClose={() => setRightPane(null)}
            />
          )}
        </div>

        {micError && (
          <div className="mic-error" role="alert">
            {micError}
          </div>
        )}

        {partialTranscript && rightPane !== "transcript" && (
          <div className="partial-transcript" aria-live="polite">
            {partialTranscript}
          </div>
        )}

        <button
          className={`sim-toggle${debugOpen ? " is-open" : ""}`}
          type="button"
          onClick={() => setDebugOpen((o) => !o)}
          aria-pressed={debugOpen}
        >
          <OptionsRegular />
          <span>{apiMode ? (api.connected ? "API" : "API…") : "Sim"}</span>
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
            slideText={slideDraft}
            onSlideTextChange={handleSlideChange}
            slideEditable={apiMode}
            onClose={() => setDebugOpen(false)}
          />
        )}
      </div>
    </FluentProvider>
  );
}

createRoot(document.getElementById("root")).render(<App />);
