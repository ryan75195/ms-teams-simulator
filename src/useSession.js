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
  }, [sessionId, apiUrl]);

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

  return {
    sessionId,
    personas,
    connected,
    view,
    send: { event: sendEvent },
    error,
  };
}
