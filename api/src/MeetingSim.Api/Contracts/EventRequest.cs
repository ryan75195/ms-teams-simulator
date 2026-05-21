using System.Text.Json.Serialization;

namespace MeetingSim.Api.Contracts;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind", IgnoreUnrecognizedTypeDiscriminators = false)]
[JsonDerivedType(typeof(SpeakEventRequest), "speak")]
[JsonDerivedType(typeof(HandRaiseEventRequest), "hand-raise")]
[JsonDerivedType(typeof(ChatMessageEventRequest), "chat")]
[JsonDerivedType(typeof(ReactionEventRequest), "reaction")]
[JsonDerivedType(typeof(SlideUpdateEventRequest), "slide-update")]
public abstract record EventRequest;
