using System.ClientModel;
using MeetingSim.Etl.Chat.Interfaces;
using OpenAI.Chat;

namespace MeetingSim.Etl.Chat;

internal sealed class OpenAIChatCompleter : IChatCompleter
{
    private readonly ChatClient _client;

    public OpenAIChatCompleter(ChatClient client)
    {
        _client = client;
    }

    public async Task<ChatCompletion> Complete(
        IEnumerable<ChatMessage> messages,
        ChatCompletionOptions? options,
        CancellationToken cancellationToken)
    {
        ClientResult<ChatCompletion> result = options is null
            ? await _client.CompleteChatAsync(messages, cancellationToken: cancellationToken).ConfigureAwait(false)
            : await _client.CompleteChatAsync(messages, options, cancellationToken).ConfigureAwait(false);
        return result.Value;
    }
}
