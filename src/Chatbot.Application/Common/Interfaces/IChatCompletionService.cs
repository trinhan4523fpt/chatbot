namespace Chatbot.Application.Common.Interfaces;

public sealed record ChatTurn(string Role, string Content); // role: system | user | assistant

/// <summary>Streaming chat completion (Ollama via Microsoft.Extensions.AI).</summary>
public interface IChatCompletionService
{
    IAsyncEnumerable<string> StreamAsync(
        IReadOnlyList<ChatTurn> messages, string model, CancellationToken ct = default);
}
