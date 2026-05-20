using System.ClientModel;
using System.Text.Json;
using MeetingSim.Core.Personas;
using OpenAI.Chat;

namespace MeetingSim.Etl.Moderator;

internal sealed class ModeratorService
{
    private const string SchemaName = "moderator_decision";

    private const string SchemaJson = """
    {
      "type": "object",
      "properties": {
        "action":     { "type": "string", "enum": ["speak", "chat", "hand-raise", "none"] },
        "personaId":  { "type": ["string", "null"] },
        "text":       { "type": ["string", "null"] },
        "raised":     { "type": ["boolean", "null"] },
        "reasoning":  { "type": "string" }
      },
      "required": ["action", "personaId", "text", "raised", "reasoning"],
      "additionalProperties": false
    }
    """;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly ChatClient _client;
    private readonly IReadOnlyList<Persona> _roster;

    public ModeratorService(ChatClient client, IReadOnlyList<Persona> roster)
    {
        _client = client;
        _roster = roster;
    }

    public async Task<ModeratorDecision> DecideAsync(
        string currentChunk,
        IReadOnlyList<string> recentChunks,
        CancellationToken cancellationToken = default)
    {
        var systemPrompt = PromptBuilder.BuildSystemPrompt(_roster);
        var userPrompt = PromptBuilder.BuildUserPrompt(currentChunk, recentChunks);

        var messages = new ChatMessage[]
        {
            new SystemChatMessage(systemPrompt),
            new UserChatMessage(userPrompt),
        };

        var options = new ChatCompletionOptions
        {
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                jsonSchemaFormatName: SchemaName,
                jsonSchema: BinaryData.FromString(SchemaJson),
                jsonSchemaIsStrict: true),
            Temperature = 0.4f,
        };

        ClientResult<ChatCompletion> completion = await _client
            .CompleteChatAsync(messages, options, cancellationToken)
            .ConfigureAwait(false);

        var payload = completion.Value.Content[0].Text;
        var decision = JsonSerializer.Deserialize<ModeratorDecision>(payload, JsonOptions)
            ?? throw new InvalidOperationException($"Moderator returned empty JSON: '{payload}'");

        return decision;
    }
}
