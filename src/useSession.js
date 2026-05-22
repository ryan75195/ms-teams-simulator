import { useCallback, useEffect, useRef, useState } from "react";
import {
  HubConnectionBuilder,
  HttpTransportType,
  LogLevel,
} from "@microsoft/signalr";
import {
  clearReaction,
  clearSpeaking,
  initialView,
  reduceEvent,
} from "./eventReducer";

const SESSION_STORAGE_KEY = "meetingSim.sessionId";
const DEFAULT_SPEAK_TTL_MS = 3500;
const REACTION_TTL_MS = 2000;

async function ensureSession(apiUrl, title, audienceSize) {
  const cached = window.localStorage.getItem(SESSION_STORAGE_KEY);
  if (cached) {
    const probe = await fetch(`${apiUrl}/sessions/${cached}`);
    if (probe.ok) return cached;
    window.localStorage.removeItem(SESSION_STORAGE_KEY);
  }
  const response = await fetch(`${apiUrl}/sessions`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ title, audienceSize }),
  });
  if (!response.ok) {
    throw new Error(`POST /sessions failed: ${response.status}`);
  }
  const session = await response.json();
  window.localStorage.setItem(SESSION_STORAGE_KEY, session.id);
  return session.id;
}

async function fetchPersonas(apiUrl, sessionId, count) {
  const response = await fetch(
    `${apiUrl}/sessions/${sessionId}/personas?count=${count}`
  );
  if (!response.ok) {
    throw new Error(`GET /personas failed: ${response.status}`);
  }
  return response.json();
}

export function useSession(apiUrl, options = {}) {
  const {
    title = "Sales Report — Q2 Review",
    audienceSize = 250,
    crowdSample = 20,
  } = options;

  const [sessionId, setSessionId] = useState(null);
  const [personas, setPersonas] = useState(null);
  const [connected, setConnected] = useState(false);
  const [error, setError] = useState(null);
  const [view, setView] = useState(initialView);
  const connectionRef = useRef(null);

  const apiUrlRef = useRef(apiUrl);
  const sessionIdRef = useRef(sessionId);
  const audioQueueRef = useRef([]);
  const isPlayingRef = useRef(false);

  useEffect(() => {
    apiUrlRef.current = apiUrl;
  }, [apiUrl]);
  useEffect(() => {
    sessionIdRef.current = sessionId;
  }, [sessionId]);

  const playNext = useCallback(() => {
    if (audioQueueRef.current.length === 0) {
      isPlayingRef.current = false;
      return;
    }
    isPlayingRef.current = true;
    const { eventId, personaId } = audioQueueRef.current.shift();
    const url = `${apiUrlRef.current}/sessions/${sessionIdRef.current}/audio/${eventId}`;
    const audio = new Audio(url);
    let settled = false;
    const settle = (clearSpeakingState) => {
      if (settled) return;
      settled = true;
      if (clearSpeakingState) {
        setView((prev) => clearSpeaking(prev, personaId));
      }
      playNext();
    };
    audio.addEventListener("ended", () => settle(true));
    audio.addEventListener("error", () => settle(false));
    audio.play().catch((e) => {
      console.warn(`Audio play failed for event ${eventId}:`, e);
      settle(false);
    });
  }, []);

  const enqueueAudio = useCallback(
    (eventId, personaId) => {
      if (!apiUrlRef.current || !sessionIdRef.current) return;
      audioQueueRef.current.push({ eventId, personaId });
      if (!isPlayingRef.current) {
        playNext();
      }
    },
    [playNext]
  );

  useEffect(() => {
    if (!apiUrl) return undefined;
    let cancelled = false;

    (async () => {
      try {
        const id = await ensureSession(apiUrl, title, audienceSize);
        if (cancelled) return;
        setSessionId(id);

        const fetched = await fetchPersonas(apiUrl, id, crowdSample);
        if (cancelled) return;
        setPersonas(fetched);
      } catch (e) {
        if (!cancelled) setError(e);
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [apiUrl, title, audienceSize, crowdSample]);

  useEffect(() => {
    if (!sessionId || !apiUrl) return undefined;

    const connection = new HubConnectionBuilder()
      .withUrl(`${apiUrl}/hubs/session`, {
        transport: HttpTransportType.WebSockets | HttpTransportType.LongPolling,
      })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    connection.on("event", (evt) => {
      setView((prev) => reduceEvent(prev, evt));

      if (evt.kind === "speak") {
        const ttl = evt.durationMs && evt.durationMs > 0 ? evt.durationMs : DEFAULT_SPEAK_TTL_MS;
        window.setTimeout(() => {
          setView((prev) => clearSpeaking(prev, evt.personaId));
        }, ttl);
        enqueueAudio(evt.id, evt.personaId);
      }

      if (evt.kind === "reaction") {
        window.setTimeout(() => {
          setView((prev) => clearReaction(prev, evt.id));
        }, REACTION_TTL_MS);
      }
    });

    let stopped = false;
    connection
      .start()
      .then(() => connection.invoke("JoinSession", sessionId))
      .then(() => {
        if (!stopped) setConnected(true);
      })
      .catch((e) => {
        if (!stopped) setError(e);
      });

    connectionRef.current = connection;

    return () => {
      stopped = true;
      setConnected(false);
      connection.stop().catch(() => {});
    };
  }, [sessionId, apiUrl, enqueueAudio]);

  const sendEvent = useCallback(
    async (body) => {
      if (!sessionId || !apiUrl) return;
      await fetch(`${apiUrl}/sessions/${sessionId}/events`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(body),
      });
    },
    [sessionId, apiUrl]
  );

  const endSession = useCallback(async () => {
    if (!sessionId || !apiUrl) return;
    try {
      await fetch(`${apiUrl}/sessions/${sessionId}`, { method: "DELETE" });
    } catch {
      // ignore — caller will re-create regardless
    }
    window.localStorage.removeItem(SESSION_STORAGE_KEY);
    try {
      const fresh = await ensureSession(apiUrl, title, audienceSize);
      setSessionId(fresh);
      const personas = await fetchPersonas(apiUrl, fresh, crowdSample);
      setPersonas(personas);
      setView(initialView);
    } catch (e) {
      setError(e);
    }
  }, [sessionId, apiUrl, title, audienceSize, crowdSample]);

  return {
    sessionId,
    personas,
    connected,
    view,
    send: { event: sendEvent },
    endSession,
    error,
  };
}
