using Chatbot.Domain.Common;
using Chatbot.Domain.Enums;

namespace Chatbot.Domain.Entities;

/// <summary>
/// A chunk of a document for a specific chunking strategy. Grain is (Document, Strategy, Index):
/// chunk text is duplicated per strategy so the chunking benchmark can compare strategies.
/// </summary>
public class DocumentChunk : CreatedEntity
{
    public long DocumentId { get; set; }
    public long ChunkingStrategyId { get; set; }
    public int ChunkIndex { get; set; }
    public string Content { get; set; } = string.Empty;
    public int? TokenCount { get; set; }
    public int? PageNumber { get; set; }
    public string? Metadata { get; set; }
    public string? ContentHash { get; set; }

    public Document Document { get; set; } = null!;
    public ChunkingStrategy ChunkingStrategy { get; set; } = null!;
    public ICollection<ChunkEmbedding> Embeddings { get; set; } = new List<ChunkEmbedding>();
}

/// <summary>
/// Links a chunk to its vector in Qdrant for a given embedding model. The vector itself lives
/// in Qdrant; this row stores only the (collection, point id) addressing + status.
/// </summary>
public class ChunkEmbedding : CreatedEntity
{
    public long ChunkId { get; set; }
    public long EmbeddingModelId { get; set; }
    public string VectorCollection { get; set; } = string.Empty;
    public Guid VectorPointId { get; set; }
    public int Dimension { get; set; }
    public ChunkEmbeddingStatus Status { get; set; } = ChunkEmbeddingStatus.Pending;
    public DateTime? IndexedAtUtc { get; set; }

    public DocumentChunk Chunk { get; set; } = null!;
    public EmbeddingModel EmbeddingModel { get; set; } = null!;
}
