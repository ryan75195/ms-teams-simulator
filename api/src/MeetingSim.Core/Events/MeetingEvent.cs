using System.Text.Json.Serialization;

namespace MeetingSim.Core.Events;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(SpeakEvent), "speak")]
[JsonDerivedType(typeof(HandRaiseEvent), "hand-raise")]
[JsonDerivedType(typeof(ChatMessageEvent), "chat")]
[JsonDerivedType(typeof(ReactionEvent), "reaction")]
[JsonDerivedType(typeof(TranscriptChunkEvent), "transcript")]
[JsonDerivedType(typeof(TranscriptMilestoneEvent), "transcript-milestone")]
[JsonDerivedType(typeof(SlideUpdateEvent), "slide-update")]
[JsonDerivedType(typeof(SilenceTickEvent), "silence-tick")]
public abstract record MeetingEvent(long Id, DateTimeOffset Ts);
