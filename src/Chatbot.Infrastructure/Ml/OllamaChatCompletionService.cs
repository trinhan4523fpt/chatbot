using System.Runtime.CompilerServices;
using Chatbot.Application.Common.Interfaces;
using Chatbot.Infrastructure.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OllamaSharp;

namespace Chatbot.Infrastructure.Ml;

public sealed class OllamaChatCompletionService : IChatCompletionService
{
    private readonly IChatClient _client;

    public OllamaChatCompletionService(IOptions<OllamaOptions> options)
    {
        var o = options.Value;
        _client = new OllamaApiClient(new Uri(o.BaseUrl), o.ChatModel);
    }

    public async IAsyncEnumerable<string> StreamAsync(
        IReadOnlyList<ChatTurn> messages, string model, ChatSamplingOptions? sampling = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var chatMessages = messages
            .Select(m => new ChatMessage(MapRole(m.Role), m.Content))
            .ToList();
        var options = new ChatOptions
        {
            ModelId = model,
            Temperature = sampling?.Temperature ?? 0.2f,
            MaxOutputTokens = sampling?.MaxOutputTokens,
        };

        await foreach (var update in _client.GetStreamingResponseAsync(chatMessages, options, ct))
        {
            if (!string.IsNullOrEmpty(update.Text))
            {
                yield return update.Text;
            }
        }
    }

    private static ChatRole MapRole(string role) => role switch
    {
        "system" => ChatRole.System,
        "assistant" => ChatRole.Assistant,
        _ => ChatRole.User,
    };
}
