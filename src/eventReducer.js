export const MAX_CHAT_HISTORY = 80;

export const initialView = {
  speaking: {},
  hands: {},
  chat: [],
  reactions: [],
  lastEventId: 0,
};

export function reduceEvent(state, event) {
  if (!event) return state;
  const lastEventId = Math.max(state.lastEventId, event.id ?? 0);

  switch (event.kind) {
    case "speak":
      return {
        ...state,
        speaking: { ...state.speaking, [event.personaId]: true },
        lastEventId,
      };
    case "hand-raise": {
      if (event.raised) {
        return {
          ...state,
          hands: { ...state.hands, [event.personaId]: true },
          lastEventId,
        };
      }
      const { [event.personaId]: _, ...remaining } = state.hands;
      return { ...state, hands: remaining, lastEventId };
    }
    case "chat":
      return {
        ...state,
        chat: [...state.chat, event].slice(-MAX_CHAT_HISTORY),
        lastEventId,
      };
    case "reaction":
      return {
        ...state,
        reactions: [
          ...state.reactions,
          { ...event, x: 25 + ((event.id * 23) % 50) },
        ],
        lastEventId,
      };
    default:
      return state;
  }
}

export function clearSpeaking(state, personaId) {
  if (!state.speaking[personaId]) return state;
  const { [personaId]: _, ...remaining } = state.speaking;
  return { ...state, speaking: remaining };
}

export function clearReaction(state, reactionId) {
  return {
    ...state,
    reactions: state.reactions.filter((r) => r.id !== reactionId),
  };
}
