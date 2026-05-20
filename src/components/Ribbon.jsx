import { useEffect, useState } from "react";
import { Tooltip } from "@fluentui/react-components";
import {
  CameraOffRegular,
  CameraRegular,
  ChatRegular,
  EmojiRegular,
  GridRegular,
  HandRightRegular,
  MicOffRegular,
  MicRegular,
  MoreHorizontalRegular,
  PeopleTeamRegular,
  ShareScreenStartRegular,
  ShareScreenStopRegular,
} from "@fluentui/react-icons";
import { formatElapsed } from "../helpers";

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

function WindowControls() {
  const api = typeof window !== "undefined" ? window.windowControls : null;
  const [isMax, setIsMax] = useState(false);

  useEffect(() => {
    if (!api) return undefined;
    let cancelled = false;
    api.isMaximized?.().then((v) => {
      if (!cancelled) setIsMax(Boolean(v));
    });
    const off = api.onMaximizedChange?.(setIsMax);
    return () => {
      cancelled = true;
      off?.();
    };
  }, [api]);

  if (!api) return null;

  return (
    <div className="window-controls" aria-label="Window controls">
      <button
        type="button"
        className="window-btn"
        onClick={api.minimize}
        aria-label="Minimize"
      >
        <svg width="10" height="10" viewBox="0 0 10 10" aria-hidden="true">
          <path d="M0 5h10" stroke="currentColor" strokeWidth="1" />
        </svg>
      </button>
      <button
        type="button"
        className="window-btn"
        onClick={api.toggleMaximize}
        aria-label={isMax ? "Restore" : "Maximize"}
      >
        {isMax ? (
          <svg width="10" height="10" viewBox="0 0 10 10" aria-hidden="true">
            <rect
              x="0.5"
              y="2.5"
              width="6.5"
              height="6.5"
              stroke="currentColor"
              fill="none"
            />
            <path
              d="M2.5 2.5V0.5H9.5V7.5H7"
              stroke="currentColor"
              fill="none"
            />
          </svg>
        ) : (
          <svg width="10" height="10" viewBox="0 0 10 10" aria-hidden="true">
            <rect
              x="0.5"
              y="0.5"
              width="9"
              height="9"
              stroke="currentColor"
              fill="none"
            />
          </svg>
        )}
      </button>
      <button
        type="button"
        className="window-btn window-btn--close"
        onClick={api.close}
        aria-label="Close"
      >
        <svg width="10" height="10" viewBox="0 0 10 10" aria-hidden="true">
          <path
            d="M0 0L10 10M10 0L0 10"
            stroke="currentColor"
            strokeWidth="1"
          />
        </svg>
      </button>
    </div>
  );
}

function TeamsAppIcon() {
  return (
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
  );
}

export function Ribbon({
  audienceSize,
  elapsed,
  rightPane,
  togglePane,
  onReact,
  handRaised,
  toggleHand,
  cameraOff,
  toggleCamera,
  muted,
  toggleMuted,
  sharing,
  toggleSharing,
}) {
  return (
    <header className="ribbon">
      <div className="ribbon-left">
        <div className="meeting-id">
          <TeamsAppIcon />
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
          onClick={() => togglePane("people")}
          badge={audienceSize}
        />
        <IconButton
          label="Chat"
          icon={<ChatRegular />}
          active={rightPane === "chat"}
          onClick={() => togglePane("chat")}
        />
        <IconButton label="React" icon={<EmojiRegular />} onClick={onReact} />
        <IconButton
          label={handRaised ? "Lower hand" : "Raise hand"}
          icon={<HandRightRegular />}
          active={handRaised}
          onClick={toggleHand}
        />
        <IconButton label="View" icon={<GridRegular />} />
        <IconButton label="More" icon={<MoreHorizontalRegular />} />
        <span className="ribbon-divider" aria-hidden="true" />
        <IconButton
          label={cameraOff ? "Turn camera on" : "Turn camera off"}
          icon={cameraOff ? <CameraOffRegular /> : <CameraRegular />}
          active={!cameraOff}
          danger={cameraOff}
          onClick={toggleCamera}
        />
        <IconButton
          label={muted ? "Unmute" : "Mute"}
          icon={muted ? <MicOffRegular /> : <MicRegular />}
          active={!muted}
          danger={muted}
          onClick={toggleMuted}
        />
        <IconButton
          label={sharing ? "Stop sharing" : "Share screen"}
          icon={sharing ? <ShareScreenStopRegular /> : <ShareScreenStartRegular />}
          active={sharing}
          onClick={toggleSharing}
        />
        <button className="leave-btn" type="button">
          Leave
        </button>
      </nav>
      <WindowControls />
    </header>
  );
}
