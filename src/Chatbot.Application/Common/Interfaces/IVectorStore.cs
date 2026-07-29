namespace Chatbot.Application.Common.Interfaces;

/// <summary>A chunk's vector + the payload stored alongside it in Qdrant.</summary>
public sealed record ChunkVector(
    Guid PointId,
    float[] Vector,
    long ChunkId,
    long DocumentId,
    long SubjectId,
    long? ChapterId,
    long ChunkingStrategyId,
    long EmbeddingModelId,
    int? Page,
    int? TokenCount);

public sealed record VectorSearchHit(long ChunkId, long DocumentId, float Score);

/// <summary>Vector store operations. The .NET API is the sole owner of all Qdrant reads and writes.</summary>
public interface IVectorStore
{
    Task EnsureCollectionAsync(string collection, int dimension, CancellationToken ct = default);

    Task UpsertChunksAsync(string collection, IReadOnlyList<ChunkVector> points, CancellationToken ct = default);

    Task<IReadOnlyList<VectorSearchHit>> SearchAsync(
        string collection, float[] queryVector, int topK, long subjectId, CancellationToken ct = default);

    Task DeleteByDocumentAsync(string collection, long documentId, CancellationToken ct = default);

    /// <summary>
    /// Removes a document's vectors from every collection of an embedding model (all chunking
    /// strategies), so a reindex under a new strategy does not leave orphaned points behind in the
    /// old collection. Returns the number of collections touched.
    /// </summary>
    Task<int> DeleteDocumentEverywhereAsync(string embeddingCollectionBase, long documentId, CancellationToken ct = default);
}
