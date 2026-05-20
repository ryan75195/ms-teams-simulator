import { Button, Slider, Switch } from "@fluentui/react-components";
import { DismissRegular } from "@fluentui/react-icons";

export function SimPanel({
  audienceSize,
  setAudienceSize,
  engagement,
  setEngagement,
  noise,
  setNoise,
  autoChat,
  setAutoChat,
  autoReactions,
  setAutoReactions,
  speakingCount,
  handsCount,
  onApplause,
  onQA,
  onClose,
}) {
  return (
    <aside className="sim-panel" aria-label="Simulator controls">
      <header className="sim-head">
        <span>SIMULATOR</span>
        <button
          className="pane-head-btn"
          aria-label="Close simulator"
          onClick={onClose}
        >
          <DismissRegular />
        </button>
      </header>
      <div className="sim-body">
        <div className="sim-stat-row">
          <div className="sim-stat">
            <strong>{audienceSize}</strong>
            <span>Attendees</span>
          </div>
          <div className="sim-stat">
            <strong>{speakingCount}</strong>
            <span>Speaking</span>
          </div>
          <div className="sim-stat">
            <strong>{handsCount}</strong>
            <span>Hands</span>
          </div>
        </div>
        <label className="sim-field">
          <span>
            Audience size <em>{audienceSize}</em>
          </span>
          <Slider
            min={9}
            max={1000}
            step={1}
            value={audienceSize}
            onChange={(_, d) => setAudienceSize(d.value)}
          />
        </label>
        <label className="sim-field">
          <span>
            Engagement <em>{engagement}/10</em>
          </span>
          <Slider
            min={1}
            max={10}
            step={1}
            value={engagement}
            onChange={(_, d) => setEngagement(d.value)}
          />
        </label>
        <label className="sim-field">
          <span>
            Background noise <em>{noise}/10</em>
          </span>
          <Slider
            min={0}
            max={10}
            step={1}
            value={noise}
            onChange={(_, d) => setNoise(d.value)}
          />
        </label>
        <div className="sim-toggles">
          <Switch
            checked={autoChat}
            onChange={(_, d) => setAutoChat(d.checked)}
            label="Auto chat"
          />
          <Switch
            checked={autoReactions}
            onChange={(_, d) => setAutoReactions(d.checked)}
            label="Auto reactions"
          />
        </div>
        <div className="sim-actions">
          <Button onClick={onApplause}>Applause surge</Button>
          <Button onClick={onQA}>Q&amp;A rush</Button>
        </div>
      </div>
    </aside>
  );
}
