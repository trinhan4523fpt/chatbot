namespace Chatbot.Application.Common.Interfaces;

public sealed record ChatTurn(string Role, string Content); // role: system | user | assistant

/// <summary>LLM sampling parameters. Null fields fall back to the model/provider default.</summary>
public sealed record ChatSamplingOptions(float? Temperature = null, int? MaxOutputTokens = null);

/// <summary>Streaming chat completion (Ollama via Microsoft.Extensions.AI).</summary>
public interface IChatCompletionService
{
    IAsyncEnumerable<string> StreamAsync(
        IReadOnlyList<ChatTurn> messages, string model, ChatSamplingOptions? options = null,
        CancellationToken ct = default);
}
