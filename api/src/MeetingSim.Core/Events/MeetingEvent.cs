using System.Text.Json.Serialization;

namespace MeetingSim.Core.Events;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(SpeakEvent), "speak")]
[JsonDerivedType(typeof(HandRaiseEvent), "hand-raise")]
[JsonDerivedType(typeof(ChatMessageEvent), "chat")]
[JsonDerivedType(typeof(ReactionEvent), "reaction")]
[JsonDerivedType(typeof(TranscriptChunkEvent), "transcript")]
public abstract record MeetingEvent(long Id, DateTimeOffset Ts);
