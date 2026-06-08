using Chatbot.Domain.Common;
using Chatbot.Domain.Enums;

namespace Chatbot.Domain.Entities;

/// <summary>A chat session scoped to a subject. Optional pins override the active system config.</summary>
public class ChatSession : AuditableEntity, ISoftDeletable
{
    public long UserId { get; set; }
    public long SubjectId { get; set; }
    public string? Title { get; set; }

    public long? PinnedEmbeddingModelId { get; set; }
    public long? PinnedChunkingStrategyId { get; set; }
    public long? PinnedLlmModelId { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public long? DeletedBy { get; set; }

    public User User { get; set; } = null!;
    public Subject Subject { get; set; } = null!;
    public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
}

/// <summary>A message in a chat session (append-only). Assistant messages start as 'streaming'.</summary>
public class ChatMessage : CreatedEntity
{
    public long SessionId { get; set; }
    public long? LlmModelId { get; set; }
    public long? EmbeddingModelId { get; set; }
    public ChatRole Role { get; set; }
    public string Content { get; set; } = string.Empty;
    public ChatMessageStatus Status { get; set; } = ChatMessageStatus.Complete;
    public int? LatencyMs { get; set; }
    public int? PromptTokens { get; set; }
    public int? CompletionTokens { get; set; }

    public ChatSession Session { get; set; } = null!;
    public ICollection<MessageCitation> Citations { get; set; } = new List<MessageCitation>();
}

/// <summary>A source citation for an assistant message. DocumentTitle is denormalized so transcripts survive soft-deletes.</summary>
public class MessageCitation : CreatedEntity
{
    public long MessageId { get; set; }
    public long? ChunkId { get; set; }
    public long? DocumentId { get; set; }
    public string? DocumentTitle { get; set; }
    public decimal? RelevanceScore { get; set; }
    public string? Snippet { get; set; }

    public ChatMessage Message { get; set; } = null!;
}
