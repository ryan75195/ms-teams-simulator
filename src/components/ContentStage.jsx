import { FILMSTRIP_SIZE, PRESENTER_INDEX } from "../constants";
import { Tile } from "./Tile";

function ContentSlide({ presenterName }) {
  return (
    <div className="content-slide">
      <header className="slide-header">
        <span className="slide-tag">Sales Report — Q2 Review</span>
        <span className="slide-page">3 / 18</span>
      </header>
      <h2>EMEA pipeline outlook</h2>
      <div className="slide-grid">
        <div className="slide-stat">
          <span className="stat-label">QoQ pipeline</span>
          <strong>+18.4%</strong>
          <span className="stat-detail">vs. +12% last quarter</span>
        </div>
        <div className="slide-stat">
          <span className="stat-label">Win rate</span>
          <strong>34%</strong>
          <span className="stat-detail">+2.1pp WoW</span>
        </div>
        <div className="slide-stat">
          <span className="stat-label">Avg. deal size</span>
          <strong>£48k</strong>
          <span className="stat-detail">+£3k MoM</span>
        </div>
      </div>
      <ul className="slide-bullets">
        <li>
          Mid-market leading EMEA growth at <strong>+24%</strong>, two large logos at LOI
        </li>
        <li>
          APAC steady; pipeline coverage at <strong>3.2×</strong> for Q3
        </li>
        <li>NAMER softness on mid-market — investigating with field team</li>
      </ul>
      <footer className="slide-presenter">
        <span className="presenter-dot" />
        <span>{presenterName} is presenting</span>
        <span className="slide-confidential">Confidential — internal only</span>
      </footer>
    </div>
  );
}

export function ContentStage({ participants, audienceSize, reactionsByTile }) {
  const filmstrip = participants.slice(0, FILMSTRIP_SIZE);
  const presenterName = participants[PRESENTER_INDEX]?.name ?? "Presenter";

  return (
    <div className="content-stage">
      <ContentSlide presenterName={presenterName} />
      <aside className="filmstrip" aria-label="Other participants">
        {filmstrip.map((person, i) => (
          <Tile
            key={person.id}
            person={person}
            reactions={reactionsByTile.get(i)}
            presenter={i === PRESENTER_INDEX}
            small
          />
        ))}
        {audienceSize > FILMSTRIP_SIZE && (
          <div className="filmstrip-more">
            <strong>+{audienceSize - FILMSTRIP_SIZE}</strong>
            <span>more</span>
          </div>
        )}
      </aside>
    </div>
  );
}
