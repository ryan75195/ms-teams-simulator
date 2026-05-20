import { useMemo, useState } from "react";
import {
  AddRegular,
  DismissRegular,
  MicOffRegular,
  MicRegular,
  MoreHorizontalRegular,
  SearchRegular,
} from "@fluentui/react-icons";
import { displayName, initials } from "../helpers";

function PersonRow({ person, highlight }) {
  return (
    <li
      className={
        `person-row${highlight ? " is-highlight" : ""}` +
        `${person.speaking ? " is-speaking" : ""}`
      }
    >
      <div className="person-avatar" style={{ background: person.color }}>
        {initials(person.name)}
      </div>
      <div className="person-info">
        <span className="person-name">{displayName(person)}</span>
        {person.you && <span className="person-tag">Organizer</span>}
      </div>
      <div className="person-icons">
        {person.hand && (
          <span className="row-hand" aria-label="Hand raised">
            ✋
          </span>
        )}
        <span
          className={`row-mic${person.muted ? " is-muted" : " is-live"}`}
          aria-label={person.muted ? "Muted" : "Unmuted"}
        >
          {person.muted ? <MicOffRegular /> : <MicRegular />}
        </span>
      </div>
    </li>
  );
}

export function PeoplePane({ participants, audienceSize, onClose }) {
  const [search, setSearch] = useState("");
  const [shownCount, setShownCount] = useState(50);

  const handsUp = useMemo(
    () => participants.filter((p) => p.hand),
    [participants]
  );
  const filtered = useMemo(() => {
    if (!search) return participants;
    const s = search.toLowerCase();
    return participants.filter((p) => p.name.toLowerCase().includes(s));
  }, [participants, search]);
  const unmuted = useMemo(
    () => participants.filter((p) => !p.muted).length,
    [participants]
  );
  const shown = filtered.slice(0, shownCount);

  return (
    <aside className="pane people-pane">
      <header className="pane-head">
        <h2>Participants</h2>
        <div className="pane-head-actions">
          <button className="pane-head-btn" aria-label="More options">
            <MoreHorizontalRegular />
          </button>
          <button
            className="pane-head-btn"
            aria-label="Close participants"
            onClick={onClose}
          >
            <DismissRegular />
          </button>
        </div>
      </header>
      <div className="pane-actions">
        <button className="primary-btn" type="button">
          <AddRegular /> Invite
        </button>
      </div>
      <label className="pane-search">
        <SearchRegular />
        <input
          placeholder="Search for participants"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
        />
      </label>
      <div className="people-scroll">
        {handsUp.length > 0 && (
          <section className="people-section">
            <header className="section-head">
              <span>Raised hands ({handsUp.length})</span>
            </header>
            <ul className="people-list">
              {handsUp.slice(0, 12).map((p) => (
                <PersonRow key={`hand-${p.id}`} person={p} highlight />
              ))}
            </ul>
          </section>
        )}
        <section className="people-section">
          <header className="section-head">
            <span>In this meeting ({audienceSize})</span>
            <span className="section-count">{unmuted} unmuted</span>
          </header>
          <ul className="people-list">
            {shown.map((p) => (
              <PersonRow key={p.id} person={p} />
            ))}
          </ul>
          {filtered.length > shownCount && (
            <button
              className="more-btn"
              type="button"
              onClick={() => setShownCount((c) => c + 100)}
            >
              Show {Math.min(100, filtered.length - shownCount)} more
            </button>
          )}
        </section>
      </div>
    </aside>
  );
}
