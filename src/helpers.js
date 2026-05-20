import { firstNames, lastNames, palette, YOU_INDEX } from "./constants";

export function initials(name) {
  return name
    .split(/\s+/)
    .filter(Boolean)
    .map((part) => part[0])
    .join("")
    .slice(0, 2)
    .toUpperCase();
}

export function formatElapsed(seconds) {
  const h = Math.floor(seconds / 3600);
  const m = String(Math.floor((seconds % 3600) / 60)).padStart(2, "0");
  const s = String(seconds % 60).padStart(2, "0");
  return h > 0 ? `${h}:${m}:${s}` : `${m}:${s}`;
}

export function formatChatTime(ts) {
  const d = new Date(ts);
  let h = d.getHours();
  const m = String(d.getMinutes()).padStart(2, "0");
  const ampm = h >= 12 ? "PM" : "AM";
  h = h % 12 || 12;
  return `${h}:${m} ${ampm}`;
}

export function makeParticipant(i) {
  const isYou = i === YOU_INDEX;
  const firstIdx = i % firstNames.length;
  const lastIdx = Math.floor(i / firstNames.length + i * 0.37) % lastNames.length;
  const colorIdx = (i * 7 + 3) % palette.length;
  const name = isYou ? "Ryan Khan" : `${firstNames[firstIdx]} ${lastNames[lastIdx]}`;
  return {
    id: i,
    name,
    you: isYou,
    color: palette[colorIdx],
    speaking: false,
    hand: false,
    muted: !isYou && Math.random() > 0.05,
  };
}

export function displayName(person) {
  return person.you ? `${person.name} (You)` : person.name;
}
