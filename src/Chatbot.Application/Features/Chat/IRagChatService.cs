namespace Chatbot.Application.Features.Chat;

/// <summary>Runs the RAG answer flow (embed query -> retrieve -> ground -> generate), streaming tokens.</summary>
public interface IRagChatService
{
    /// <param name="onReset">Signals the client to discard tokens streamed so far (answer is being regenerated).</param>
    Task<ChatAnswerResult> AnswerAsync(
        long sessionId,
        long userId,
        IReadOnlyCollection<string> roles,
        string question,
        Func<string, Task> onToken,
        Func<Task> onReset,
        CancellationToken ct);
}
