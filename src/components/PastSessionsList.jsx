import { useCallback, useEffect, useState } from "react";
import {
  ArrowDownloadRegular,
  ArrowSyncRegular,
  DocumentTextRegular,
} from "@fluentui/react-icons";

function formatRelative(ts) {
  const then = new Date(ts).getTime();
  const ms = Date.now() - then;
  if (Number.isNaN(ms)) return "?";
  const minutes = Math.floor(ms / 60_000);
  if (minutes < 1) return "just now";
  if (minutes < 60) return `${minutes}m ago`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${hours}h ago`;
  const days = Math.floor(hours / 24);
  return `${days}d ago`;
}

function formatAbsolute(ts) {
  try {
    return new Date(ts).toLocaleString();
  } catch {
    return ts;
  }
}

export function PastSessionsList({ apiUrl, visible, currentSessionId }) {
  const [items, setItems] = useState([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);

  const reload = useCallback(async () => {
    if (!apiUrl) return;
    setLoading(true);
    setError(null);
    try {
      const response = await fetch(`${apiUrl}/sessions/archived`);
      if (!response.ok) throw new Error(`GET /sessions/archived ${response.status}`);
      const data = await response.json();
      setItems(Array.isArray(data) ? data : []);
    } catch (e) {
      setError(e.message || String(e));
    } finally {
      setLoading(false);
    }
  }, [apiUrl]);

  useEffect(() => {
    if (visible) reload();
  }, [visible, reload]);

  if (!apiUrl) {
    return null;
  }

  return (
    <section className="past-sessions">
      <header className="past-sessions-head">
        <span>Past sessions</span>
        <button
          type="button"
          className="past-sessions-refresh"
          onClick={reload}
          aria-label="Refresh past sessions"
          title="Refresh"
          disabled={loading}
        >
          <ArrowSyncRegular />
        </button>
      </header>
      {error && <div className="past-sessions-error">{error}</div>}
      {!error && !loading && items.length === 0 && (
        <div className="past-sessions-empty">
          Nothing archived yet. End a session with <strong>Leave</strong> and it'll appear here.
        </div>
      )}
      {items.length > 0 && (
        <ul className="past-sessions-list">
          {items.map((m) => (
            <PastSessionRow
              key={m.id}
              manifest={m}
              apiUrl={apiUrl}
              isCurrent={m.id === currentSessionId}
            />
          ))}
        </ul>
      )}
    </section>
  );
}

function PastSessionRow({ manifest, apiUrl, isCurrent }) {
  const ended = manifest.endedAt;
  const status = ended ? `Ended ${formatRelative(ended)}` : isCurrent ? "Live (this session)" : "Live";
  return (
    <li className={`past-session-row${isCurrent ? " is-current" : ""}`}>
      <div className="past-session-meta">
        <strong title={manifest.id}>{manifest.title}</strong>
        <span title={formatAbsolute(manifest.startedAt)}>
          Started {formatRelative(manifest.startedAt)} · {status}
        </span>
      </div>
      <div className="past-session-actions">
        <a
          href={`${apiUrl}/sessions/${manifest.id}/transcript.md`}
          target="_blank"
          rel="noreferrer"
          title="Open transcript"
          aria-label="Open transcript"
        >
          <DocumentTextRegular />
        </a>
        <a
          href={`${apiUrl}/sessions/${manifest.id}/archive.zip`}
          target="_blank"
          rel="noreferrer"
          title="Download archive"
          aria-label="Download archive"
        >
          <ArrowDownloadRegular />
        </a>
      </div>
    </li>
  );
}
