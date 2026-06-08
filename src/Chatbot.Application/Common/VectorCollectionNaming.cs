namespace Chatbot.Application.Common;

/// <summary>
/// Qdrant collection naming: one collection per (embedding model base slug, chunking strategy id).
/// Surrogate-key based, collision-free, FK-aligned — never derived from dimension or human params.
/// </summary>
public static class VectorCollectionNaming
{
    public static string For(string embeddingCollectionBase, long chunkingStrategyId) =>
        $"{embeddingCollectionBase}__strat_{chunkingStrategyId}";
}
