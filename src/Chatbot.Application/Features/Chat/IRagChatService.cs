namespace Chatbot.Application.Features.Chat;

/// <summary>Runs the RAG answer flow (embed query -> retrieve -> ground -> generate), streaming tokens.</summary>
public interface IRagChatService
{
    Task<ChatAnswerResult> AnswerAsync(
        long sessionId,
        long userId,
        IReadOnlyCollection<string> roles,
        string question,
        Func<string, Task> onToken,
        CancellationToken ct);
}
