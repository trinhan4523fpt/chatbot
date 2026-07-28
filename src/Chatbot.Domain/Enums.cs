namespace Chatbot.Domain.Enums;

// Enum members are PascalCase; they are persisted (and serialized over the API) as
// snake_case tokens (e.g. RagVsFinetune <-> "rag_vs_finetune") via a shared converter,
// matching the DB CHECK constraints.

public enum FileType
{
    Pdf,
    Docx,
    Slide,
}

public enum DocumentStatus
{
    Uploaded,
    Processing,
    Indexed,
    Failed,
}

public enum ProcessingStage
{
    Parse,
    Chunk,
    Embed,
    Index,
    Complete,
}

public enum ProcessingState
{
    Queued,
    Running,
    Succeeded,
    Failed,
    Cancelled,
}

public enum ChunkEmbeddingStatus
{
    Pending,
    Indexed,
    Failed,
    Stale,
}

public enum ChatRole
{
    User,
    Assistant,
    System,
}

public enum ChatMessageStatus
{
    Complete,
    Streaming,
    Cancelled,
    Error,
}

public enum LlmModelType
{
    Rag,
    FineTuned,
    Base,
}

public enum ExperimentType
{
    RagVsFinetune,
    ChunkingBench,
    EmbeddingBench,
}

public enum ExperimentStatus
{
    Draft,
    Running,
    Done,
}

public enum RunStatus
{
    Queued,
    Running,
    Done,
    Error,
    Skipped,
}

public enum PerQuestionStatus
{
    Pending,
    Done,
    Error,
}

public enum Difficulty
{
    Easy,
    Medium,
    Hard,
}

public enum OutboxStatus
{
    Pending,
    Processing,
    Done,
    Failed,
}

public enum OutboxEventType
{
    QdrantUpsert,
    QdrantDelete,
}

// ── Payment / Token ──────────────────────────────────────────────────────────

/// <summary>Trạng thái đơn hàng mua gói token.</summary>
public enum OrderStatus
{
    /// <summary>Chờ thanh toán.</summary>
    Pending,
    /// <summary>Thanh toán thành công, token đã được cộng vào ví.</summary>
    Paid,
    /// <summary>Đơn hàng đã hết hạn chờ (quá 15 phút không thanh toán).</summary>
    Expired,
    /// <summary>Thanh toán thất bại (VnPay trả lỗi).</summary>
    Failed,
    /// <summary>Đã hoàn tiền.</summary>
    Refunded,
}

/// <summary>Loại giao dịch token trong ví.</summary>
public enum TokenTransactionType
{
    /// <summary>Cộng token khi mua gói thành công.</summary>
    Purchase,
    /// <summary>Trừ token mỗi lần dùng chatbot.</summary>
    ChatUsage,
    /// <summary>Admin cộng/trừ thủ công (điều chỉnh).</summary>
    AdminAdjustment,
    /// <summary>Hoàn token do refund.</summary>
    Refund,
    /// <summary>Token hết hạn, hệ thống xoá số dư.</summary>
    Expiry,
}
