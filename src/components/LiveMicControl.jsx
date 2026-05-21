import { useEffect, useRef, useState } from "react";
import { useLiveMic } from "../audio/useLiveMic";

const ERROR_DISPLAY_MS = 6000;

export function LiveMicControl({ enabled, wsUrl, onPartial }) {
  const [error, setError] = useState(null);
  const errorTimerRef = useRef(null);

  function reportError(message) {
    setError(message);
    if (errorTimerRef.current) window.clearTimeout(errorTimerRef.current);
    errorTimerRef.current = window.setTimeout(
      () => setError(null),
      ERROR_DISPLAY_MS
    );
  }

  const mic = useLiveMic({
    wsUrl,
    onPartial,
    onError: reportError,
  });

  useEffect(
    () => () => {
      if (errorTimerRef.current) window.clearTimeout(errorTimerRef.current);
    },
    []
  );

  if (!enabled) return null;

  const isLive = mic.state === "live";
  const isStarting = mic.state === "starting";
  const label = isLive ? "Stop" : isStarting ? "Starting…" : "Live mic";

  return (
    <>
      <button
        type="button"
        className={
          `rec-toggle${isLive ? " is-recording" : ""}` +
          `${isStarting ? " is-uploading" : ""}`
        }
        onClick={() => {
          if (isLive) mic.stop();
          else mic.start();
        }}
        disabled={isStarting}
        aria-pressed={isLive}
        aria-label={label}
      >
        <span className="rec-toggle-dot" aria-hidden="true" />
        <span>{label}</span>
      </button>
      {error && (
        <div className="rec-error" role="alert">
          {error}
        </div>
      )}
    </>
  );
}
