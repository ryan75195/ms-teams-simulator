import { MicOffRegular } from "@fluentui/react-icons";
import { displayName, initials } from "../helpers";

export function Tile({ person, reactions, small, presenter }) {
  return (
    <div
      className={
        `tile${small ? " tile--small" : ""}` +
        `${person.speaking ? " is-speaking" : ""}` +
        `${person.hand ? " is-hand" : ""}` +
        `${presenter ? " is-presenter" : ""}`
      }
    >
      <div className="tile-canvas">
        <div className="tile-avatar" style={{ background: person.color }}>
          {initials(person.name)}
        </div>
      </div>
      {person.hand && (
        <span className="tile-hand" aria-label="Hand raised">
          ✋
        </span>
      )}
      {presenter && <span className="tile-presenter-tag">Presenting</span>}
      <div className="tile-foot">
        {person.muted && (
          <span className="tile-mic" aria-label="Muted">
            <MicOffRegular />
          </span>
        )}
        <span className="tile-name">{displayName(person)}</span>
      </div>
      <div className="tile-reactions" aria-hidden="true">
        {reactions?.map((r) => (
          <span key={r.id} className="reaction" style={{ "--x": `${r.x}%` }}>
            {r.emoji}
          </span>
        ))}
      </div>
    </div>
  );
}
