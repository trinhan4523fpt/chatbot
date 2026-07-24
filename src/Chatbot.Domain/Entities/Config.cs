using Chatbot.Domain.Common;
using Chatbot.Domain.Enums;

namespace Chatbot.Domain.Entities;

/// <summary>A chunking strategy (fixed-size, recursive, semantic, ...). Params is JSON.</summary>
public class ChunkingStrategy : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public int? ChunkSize { get; set; }
    public int? ChunkOverlap { get; set; }
    public string? Params { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>A registered embedding model. Dimension is reconciled against the Python ML /models endpoint.</summary>
public class EmbeddingModel : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Provider { get; set; }
    public int Dimension { get; set; }
    public bool IsFree { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public int? MaxInputTokens { get; set; }

    /// <summary>Stable per-model Qdrant collection slug — the single source of truth, never derived from dimension.</summary>
    public string QdrantCollectionName { get; set; } = string.Empty;
    public string? Description { get; set; }
}

/// <summary>A registered LLM (rag / fine_tuned / base), served via Ollama.</summary>
public class LlmModel : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public LlmModelType Type { get; set; }
    public string? Provider { get; set; }
    public string? BaseModel { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Description { get; set; }
}

/// <summary>Singleton (Id = 1) holding the active production RAG configuration and tunables.</summary>
public class SystemConfiguration : AuditableEntity
{
    public long? ActiveEmbeddingModelId { get; set; }
    public long? ActiveChunkingStrategyId { get; set; }
    public long? ActiveLlmModelId { get; set; }

    /// <summary>Overrides the active chunking strategy's ChunkSize (in tokens). Null = use the strategy's own.</summary>
    public int? ActiveChunkSize { get; set; }

    /// <summary>Overrides the active chunking strategy's ChunkOverlap (in tokens). Null = use the strategy's own.</summary>
    public int? ActiveChunkOverlap { get; set; }

    public int RetrievalTopK { get; set; } = 5;
    public decimal MinRelevanceScore { get; set; } = 0.30m;
    public bool ScopeRestriction { get; set; } = true;
    public string? PromptTemplate { get; set; }

    /// <summary>LLM sampling temperature (0 = deterministic, higher = more creative).</summary>
    public decimal Temperature { get; set; } = 0.2m;

    /// <summary>Max tokens the LLM may generate per answer. Null = model default.</summary>
    public int? MaxOutputTokens { get; set; }

    public long MaxUploadBytes { get; set; } = 50L * 1024 * 1024;
    public int HistoryWindowTurns { get; set; } = 10;

    public int LockoutMaxAttempts { get; set; } = 5;
    public int LockoutMinutes { get; set; } = 15;
    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 7;

    public EmbeddingModel? ActiveEmbeddingModel { get; set; }
    public ChunkingStrategy? ActiveChunkingStrategy { get; set; }
    public LlmModel? ActiveLlmModel { get; set; }
}

/// <summary>Miscellaneous feature-flag style settings (structured config lives in SystemConfiguration).</summary>
public class AppSetting : Entity
{
    public string Key { get; set; } = string.Empty;
    public string? Value { get; set; }
    public string? ValueType { get; set; }
    public string? Description { get; set; }
}
