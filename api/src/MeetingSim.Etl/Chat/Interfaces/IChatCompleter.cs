using OpenAI.Chat;

namespace MeetingSim.Etl.Chat.Interfaces;

public interface IChatCompleter
{
    Task<ChatCompletion> Complete(
        IEnumerable<ChatMessage> messages,
        ChatCompletionOptions? options,
        CancellationToken cancellationToken);
}
