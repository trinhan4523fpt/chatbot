using Chatbot.Domain.Enums;

namespace Chatbot.Application.Features.Chat;

public sealed record ChatSessionDto(
    long Id, long SubjectId, string? Title, DateTime CreatedAtUtc, DateTime? UpdatedAtUtc);

public sealed record ChatCitationDto(
    long? ChunkId, long? DocumentId, string? DocumentTitle, decimal? Score, string? Snippet);

public sealed record ChatMessageDto(
    long Id, ChatRole Role, string Content, ChatMessageStatus Status, int? LatencyMs,
    DateTime CreatedAtUtc, IReadOnlyList<ChatCitationDto> Citations);

public sealed record ChatAnswerResult(
    long AssistantMessageId, bool ScopeRestricted, string Content, int LatencyMs,
    IReadOnlyList<ChatCitationDto> Citations);
