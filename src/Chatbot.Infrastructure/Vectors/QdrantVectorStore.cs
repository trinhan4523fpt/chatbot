using Chatbot.Application.Common.Interfaces;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace Chatbot.Infrastructure.Vectors;

public sealed class QdrantVectorStore(QdrantClient client) : IVectorStore
{
    public async Task EnsureCollectionAsync(string collection, int dimension, CancellationToken ct = default)
    {
        if (await client.CollectionExistsAsync(collection, ct))
        {
            return;
        }

        await client.CreateCollectionAsync(
            collection,
            new VectorParams { Size = (ulong)dimension, Distance = Distance.Cosine },
            cancellationToken: ct);

        await client.CreatePayloadIndexAsync(collection, "subjectId", PayloadSchemaType.Integer, cancellationToken: ct);
        await client.CreatePayloadIndexAsync(collection, "documentId", PayloadSchemaType.Integer, cancellationToken: ct);
        await client.CreatePayloadIndexAsync(collection, "chunkingStrategyId", PayloadSchemaType.Integer, cancellationToken: ct);
    }

    public async Task UpsertChunksAsync(string collection, IReadOnlyList<ChunkVector> points, CancellationToken ct = default)
    {
        if (points.Count == 0)
        {
            return;
        }

        var structs = new List<PointStruct>(points.Count);
        foreach (var c in points)
        {
            var point = new PointStruct { Id = c.PointId, Vectors = c.Vector };
            point.Payload["chunkId"] = c.ChunkId;
            point.Payload["documentId"] = c.DocumentId;
            point.Payload["subjectId"] = c.SubjectId;
            point.Payload["chunkingStrategyId"] = c.ChunkingStrategyId;
            point.Payload["embeddingModelId"] = c.EmbeddingModelId;
            if (c.ChapterId is { } chapterId)
            {
                point.Payload["chapterId"] = chapterId;
            }

            if (c.Page is { } page)
            {
                point.Payload["page"] = (long)page;
            }

            if (c.TokenCount is { } tokenCount)
            {
                point.Payload["tokenCount"] = (long)tokenCount;
            }

            structs.Add(point);
        }

        await client.UpsertAsync(collection, structs, cancellationToken: ct);
    }

    public async Task<IReadOnlyList<VectorSearchHit>> SearchAsync(
        string collection, float[] queryVector, int topK, long subjectId, CancellationToken ct = default)
    {
        var filter = new Filter();
        filter.Must.Add(Conditions.Match("subjectId", subjectId));

        var results = await client.QueryAsync(
            collection,
            query: queryVector,
            filter: filter,
            limit: (ulong)topK,
            payloadSelector: true,
            cancellationToken: ct);

        var hits = new List<VectorSearchHit>(results.Count);
        foreach (var point in results)
        {
            hits.Add(new VectorSearchHit(
                point.Payload.TryGetValue("chunkId", out var chunkId) ? chunkId.IntegerValue : 0,
                point.Payload.TryGetValue("documentId", out var documentId) ? documentId.IntegerValue : 0,
                point.Score));
        }

        return hits;
    }

    public async Task DeleteByDocumentAsync(string collection, long documentId, CancellationToken ct = default)
    {
        if (!await client.CollectionExistsAsync(collection, ct))
        {
            return;
        }

        var filter = new Filter();
        filter.Must.Add(Conditions.Match("documentId", documentId));
        await client.DeleteAsync(collection, filter, cancellationToken: ct);
    }
}
