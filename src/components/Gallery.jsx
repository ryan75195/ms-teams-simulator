import { Tile } from "./Tile";

export function Gallery({ participants, reactionsByTile }) {
  return (
    <div className="gallery">
      {participants.map((person, i) => (
        <Tile key={person.id} person={person} reactions={reactionsByTile.get(i)} />
      ))}
    </div>
  );
}
