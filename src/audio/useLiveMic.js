import { useCallback, useEffect, useRef, useState } from "react";
import workletUrl from "./pcmWorklet.js?url";

const TARGET_SAMPLE_RATE = 24000;
const IDLE_TIMEOUT_MS = 90_000;
const IDLE_CHECK_INTERVAL_MS = 1_000;
const SILENCE_TICK_THRESHOLD_MS = 4_000;
const SILENCE_TICK_COOLDOWN_MS = 20_000;
const AUDIO_THRESHOLD_PCM16 = 1500;

export function useLiveMic({ wsUrl, onPartial, onError, onSilence }) {
  const [state, setState] = useState("idle");
  const runningRef = useRef(false);
  const ctxRef = useRef(null);
  const wsRef = useRef(null);
  const streamRef = useRef(null);
  const nodeRef = useRef(null);
  const sourceRef = useRef(null);
  const lastAudioAtRef = useRef(0);
  const silenceFiredRef = useRef(false);
  const lastSilenceFiredAtRef = useRef(0);
  const idleCheckRef = useRef(null);

  const stop = useCallback(() => {
    if (!runningRef.current) return;
    runningRef.current = false;

    if (idleCheckRef.current) {
      window.clearInterval(idleCheckRef.current);
      idleCheckRef.current = null;
    }

    sourceRef.current?.disconnect();
    sourceRef.current = null;

    nodeRef.current?.port?.close();
    nodeRef.current?.disconnect();
    nodeRef.current = null;

    streamRef.current?.getTracks().forEach((track) => track.stop());
    streamRef.current = null;

    if (ctxRef.current && ctxRef.current.state !== "closed") {
      ctxRef.current.close().catch(() => {});
    }
    ctxRef.current = null;

    if (wsRef.current && wsRef.current.readyState <= WebSocket.OPEN) {
      try {
        wsRef.current.close(1000, "user_stop");
      } catch {
        // ignored
      }
    }
    wsRef.current = null;

    setState("idle");
  }, []);

  const start = useCallback(async () => {
    if (runningRef.current) return;
    if (!wsUrl) {
      onError?.("No session WebSocket URL.");
      return;
    }
    runningRef.current = true;
    setState("starting");
    try {
      const stream = await navigator.mediaDevices.getUserMedia({
        audio: {
          channelCount: 1,
          echoCancellation: true,
          noiseSuppression: true,
          autoGainControl: true,
        },
      });
      if (!runningRef.current) {
        stream.getTracks().forEach((t) => t.stop());
        return;
      }
      streamRef.current = stream;

      const ctx = new AudioContext({ sampleRate: TARGET_SAMPLE_RATE });
      ctxRef.current = ctx;
      await ctx.audioWorklet.addModule(workletUrl);

      const ws = new WebSocket(wsUrl);
      ws.binaryType = "arraybuffer";
      wsRef.current = ws;

      ws.onmessage = (e) => {
        try {
          const msg = JSON.parse(e.data);
          if (msg.type === "transcript.partial") onPartial?.(msg.text);
        } catch {
          // ignored
        }
      };
      ws.onerror = () => {
        if (runningRef.current) {
          onError?.("WebSocket error.");
          stop();
        }
      };
      ws.onclose = () => {
        if (runningRef.current) stop();
      };

      await new Promise((resolve, reject) => {
        ws.addEventListener("open", () => resolve(), { once: true });
        ws.addEventListener(
          "error",
          () => reject(new Error("WebSocket failed to open")),
          { once: true }
        );
      });

      if (!runningRef.current) return;

      const source = ctx.createMediaStreamSource(stream);
      const node = new AudioWorkletNode(ctx, "pcm-downsampler");
      sourceRef.current = source;
      nodeRef.current = node;

      node.port.onmessage = (e) => {
        if (frameHasAudio(e.data)) {
          lastAudioAtRef.current = Date.now();
          silenceFiredRef.current = false;
        }
        if (wsRef.current && wsRef.current.readyState === WebSocket.OPEN) {
          wsRef.current.send(e.data);
        }
      };
      source.connect(node);

      lastAudioAtRef.current = Date.now();
      silenceFiredRef.current = false;
      idleCheckRef.current = window.setInterval(() => {
        if (!runningRef.current) return;
        const gap = Date.now() - lastAudioAtRef.current;
        if (gap >= IDLE_TIMEOUT_MS) {
          onError?.(`Live mic auto-paused after ${IDLE_TIMEOUT_MS / 1000}s of silence — click to resume.`);
          stop();
          return;
        }
        if (gap >= SILENCE_TICK_THRESHOLD_MS && !silenceFiredRef.current) {
          const now = Date.now();
          if (now - lastSilenceFiredAtRef.current >= SILENCE_TICK_COOLDOWN_MS) {
            silenceFiredRef.current = true;
            lastSilenceFiredAtRef.current = now;
            onSilence?.(Math.floor(gap / 1000));
          }
        }
      }, IDLE_CHECK_INTERVAL_MS);

      setState("live");
    } catch (e) {
      onError?.(e?.message ?? String(e));
      stop();
    }
  }, [wsUrl, onPartial, onError, onSilence, stop]);

  useEffect(
    () => () => {
      runningRef.current = false;
      stop();
    },
    [stop]
  );

  return { state, start, stop };
}

function frameHasAudio(arrayBuffer) {
  const samples = new Int16Array(arrayBuffer);
  for (let i = 0; i < samples.length; i += 1) {
    const v = samples[i];
    if (v > AUDIO_THRESHOLD_PCM16 || v < -AUDIO_THRESHOLD_PCM16) {
      return true;
    }
  }
  return false;
}
