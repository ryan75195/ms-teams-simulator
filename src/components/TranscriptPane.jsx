import { useEffect, useRef } from "react";
import { DismissRegular } from "@fluentui/react-icons";

export function TranscriptPane({ transcripts, partial, onClose }) {
  const scrollRef = useRef(null);

  useEffect(() => {
    const el = scrollRef.current;
    if (!el) return;
    el.scrollTop = el.scrollHeight;
  }, [transcripts.length, partial]);

  const empty = transcripts.length === 0 && !partial;

  return (
    <aside className="pane transcript-pane" aria-label="Live transcript">
      <header className="pane-head">
        <h2>Transcript</h2>
        <div className="pane-head-actions">
          <button
            className="pane-head-btn"
            aria-label="Close transcript"
            onClick={onClose}
          >
            <DismissRegular />
          </button>
        </div>
      </header>

      <div className="transcript-scroll" ref={scrollRef}>
        {empty && (
          <div className="transcript-empty">
            Nothing transcribed yet. Click <strong>Live mic</strong> and start speaking.
          </div>
        )}
        <ol className="transcript-list">
          {transcripts.map((t) => (
            <li className="transcript-item" key={t.id}>
              <span className="transcript-text">{t.text}</span>
            </li>
          ))}
          {partial && (
            <li className="transcript-item transcript-item--partial" aria-live="polite">
              <span className="transcript-text">{partial}</span>
            </li>
          )}
        </ol>
      </div>
    </aside>
  );
}
