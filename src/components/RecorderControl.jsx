import { useEffect, useRef, useState } from "react";

const RECORDER_MIME_FALLBACK = "audio/webm";
const ERROR_DISPLAY_MS = 6000;

export function RecorderControl({ enabled, onRecorded }) {
  const [state, setState] = useState("idle");
  const [error, setError] = useState(null);
  const recorderRef = useRef(null);
  const chunksRef = useRef([]);
  const streamRef = useRef(null);
  const errorTimerRef = useRef(null);

  useEffect(() => {
    return () => {
      if (streamRef.current) {
        streamRef.current.getTracks().forEach((track) => track.stop());
        streamRef.current = null;
      }
      if (errorTimerRef.current) {
        window.clearTimeout(errorTimerRef.current);
      }
    };
  }, []);

  function reportError(message) {
    setError(message);
    if (errorTimerRef.current) {
      window.clearTimeout(errorTimerRef.current);
    }
    errorTimerRef.current = window.setTimeout(() => setError(null), ERROR_DISPLAY_MS);
  }

  async function startRecording() {
    if (state !== "idle") return;
    if (!navigator.mediaDevices?.getUserMedia) {
      reportError("Mic API unavailable in this browser.");
      return;
    }
    try {
      const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
      streamRef.current = stream;
      chunksRef.current = [];

      const recorder = new MediaRecorder(stream);
      recorder.ondataavailable = (event) => {
        if (event.data && event.data.size > 0) {
          chunksRef.current.push(event.data);
        }
      };
      recorder.onstop = async () => {
        streamRef.current?.getTracks().forEach((track) => track.stop());
        streamRef.current = null;

        const mimeType = recorder.mimeType || RECORDER_MIME_FALLBACK;
        const blob = new Blob(chunksRef.current, { type: mimeType });
        chunksRef.current = [];

        if (blob.size === 0) {
          reportError("Empty recording — no audio captured.");
          setState("idle");
          return;
        }

        setState("uploading");
        try {
          await onRecorded(blob);
          setError(null);
        } catch (e) {
          console.warn("Transcribe upload failed:", e);
          reportError(`Upload failed: ${e?.message ?? e}`);
        } finally {
          setState("idle");
        }
      };

      recorderRef.current = recorder;
      recorder.start();
      setError(null);
      setState("recording");
    } catch (e) {
      console.warn("Microphone access denied or unavailable:", e);
      const name = e?.name ?? "Error";
      const detail = e?.message ?? String(e);
      reportError(`${name}: ${detail}`);
      setState("idle");
    }
  }

  function stopRecording() {
    if (state !== "recording") return;
    recorderRef.current?.stop();
  }

  if (!enabled) return null;

  const isRecording = state === "recording";
  const isUploading = state === "uploading";
  const label = isRecording ? "Stop" : isUploading ? "Transcribing…" : "Record";

  return (
    <>
      <button
        type="button"
        className={
          `rec-toggle${isRecording ? " is-recording" : ""}${isUploading ? " is-uploading" : ""}`
        }
        onClick={isRecording ? stopRecording : startRecording}
        disabled={isUploading}
        aria-pressed={isRecording}
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
