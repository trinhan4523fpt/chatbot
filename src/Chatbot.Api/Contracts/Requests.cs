namespace Chatbot.Api.Contracts;

public sealed record LoginRequest(string Email, string Password);

public sealed record RefreshRequest(string RefreshToken);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public sealed record CreateUserRequest(string Email, string FullName, string? Password, IReadOnlyList<string>? Roles);

public sealed record AssignRolesRequest(IReadOnlyList<string> Roles);

public sealed record ResetPasswordRequest(string NewPassword);

public sealed record SetActiveRequest(bool IsActive);

public sealed record CreateChatSessionRequest(long SubjectId, string? Title);

public sealed record SendMessageRequest(string Content);

public sealed record ConfirmEmailRequest(string Email, string Code);

/// <summary>Partial update of the active RAG configuration; null fields are left unchanged.</summary>
public sealed record UpdateSystemConfigurationRequest(
    long? ActiveEmbeddingModelId,
    long? ActiveChunkingStrategyId,
    int? ActiveChunkSize,
    int? ActiveChunkOverlap,
    long? ActiveLlmModelId,
    int? RetrievalTopK,
    decimal? MinRelevanceScore,
    bool? ScopeRestriction,
    string? PromptTemplate,
    int? HistoryWindowTurns,
    decimal? Temperature,
    int? MaxOutputTokens,
    bool ReindexNow = false);
