using Chatbot.Application.Common;
using Chatbot.Application.Common.Exceptions;
using Chatbot.Application.Common.Interfaces;
using Chatbot.Domain.Entities;
using Chatbot.Domain.Enums;
using Chatbot.Infrastructure.Persistence;
using Chatbot.Infrastructure.Vectors;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Chatbot.Infrastructure.Jobs;

/// <summary>
/// Stage-based, idempotent ingestion: parse -> chunk -> embed -> index (Qdrant) -> mark indexed.
/// Re-runs cleanly (deterministic point ids, per-(doc,strategy) chunk rewrite, Qdrant delete-by-document).
/// </summary>
public sealed class IngestDocumentJob(
    ChatbotDbContext db,
    IAiServiceClient ai,
    IVectorStore vectors,
    IFileStorageService storage,
    IClock clock,
    ILogger<IngestDocumentJob> logger)
{
    private const int EmbedBatchSize = 64;

    [AutomaticRetry(Attempts = 3)]
    public async Task RunAsync(long documentId, CancellationToken ct)
    {
        var document = await db.Documents.IgnoreQueryFilters().FirstOrDefaultAsync(d => d.Id == documentId, ct);
        if (document is null || document.IsDeleted)
        {
            logger.LogWarning("Ingestion skipped: document {DocumentId} not found or deleted.", documentId);
            return;
        }

        var cfg = await db.SystemConfigurations.AsNoTracking().FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("System configuration is missing.");
        var embeddingModel = await db.EmbeddingModels.FirstAsync(m => m.Id == cfg.ActiveEmbeddingModelId, ct);
        var strategy = await db.ChunkingStrategies.FirstAsync(s => s.Id == cfg.ActiveChunkingStrategyId, ct);

        var job = await db.DocumentProcessingJobs
            .Where(j => j.DocumentId == documentId &&
                        (j.State == ProcessingState.Queued || j.State == ProcessingState.Running))
            .OrderByDescending(j => j.Id)
            .FirstOrDefaultAsync(ct);
        if (job is null)
        {
            job = new DocumentProcessingJob { DocumentId = documentId };
            db.DocumentProcessingJobs.Add(job);
        }

        job.EmbeddingModelId = embeddingModel.Id;
        job.ChunkingStrategyId = strategy.Id;
        job.State = ProcessingState.Running;
        job.Stage = ProcessingStage.Parse;
        job.AttemptCount++;
        job.StartedAtUtc ??= clock.UtcNow;
        job.UpdatedAtUtc = clock.UtcNow;
        document.Status = DocumentStatus.Processing;
        await db.SaveChangesAsync(ct);

        try
        {
            // 1) Parse (file bytes -> pages) via Python.
            var bytes = await File.ReadAllBytesAsync(storage.ResolvePhysicalPath(document.RelativePath), ct);
            var parsed = await ai.ParseAsync(bytes, document.OriginalFileName, ct);
            document.PageCount = parsed.PageCount;

            // 2) Chunk via Python.
            await AdvanceStageAsync(job, ProcessingStage.Chunk, ct);
            var chunks = await ai.ChunkAsync(
                parsed.Pages, strategy.Name, strategy.ChunkSize ?? 512, strategy.ChunkOverlap ?? 50, ct);
            if (chunks.Count == 0)
            {
                throw new InvalidOperationException("Không trích xuất được nội dung văn bản từ tài liệu.");
            }

            // 3) Persist chunks atomically per (document, strategy) — cascades delete old embeddings.
            var existing = await db.DocumentChunks
                .Where(c => c.DocumentId == documentId && c.ChunkingStrategyId == strategy.Id)
                .ToListAsync(ct);
            db.DocumentChunks.RemoveRange(existing);
            await db.SaveChangesAsync(ct);

            var chunkEntities = chunks
                .Select(c => new DocumentChunk
                {
                    DocumentId = documentId,
                    ChunkingStrategyId = strategy.Id,
                    ChunkIndex = c.Index,
                    Content = c.Content,
                    PageNumber = c.Page,
                    TokenCount = c.TokenCount,
                })
                .ToList();
            db.DocumentChunks.AddRange(chunkEntities);
            await db.SaveChangesAsync(ct);

            // 4) Embed (Python) + 5) index into Qdrant (.NET-owned).
            await AdvanceStageAsync(job, ProcessingStage.Embed, ct);
            var collection = VectorCollectionNaming.For(embeddingModel.QdrantCollectionName, strategy.Id);
            await vectors.EnsureCollectionAsync(collection, embeddingModel.Dimension, ct);
            await vectors.DeleteByDocumentAsync(collection, documentId, ct);

            var chunkVectors = new List<ChunkVector>(chunkEntities.Count);
            foreach (var batch in chunkEntities.Chunk(EmbedBatchSize))
            {
                var embedding = await ai.EmbedAsync(
                    batch.Select(c => c.Content).ToList(), embeddingModel.Name, "passage", ct);
                if (embedding.Dim != embeddingModel.Dimension)
                {
                    throw new InvalidOperationException(
                        $"Embedding dimension mismatch for '{embeddingModel.Name}': expected {embeddingModel.Dimension}, got {embedding.Dim}.");
                }

                for (var i = 0; i < batch.Length; i++)
                {
                    var chunk = batch[i];
                    var pointId = Vectors.PointIds.For(chunk.Id, embeddingModel.Id);
                    chunkVectors.Add(new ChunkVector(
                        pointId, embedding.Vectors[i], chunk.Id, documentId, document.SubjectId,
                        document.ChapterId, strategy.Id, embeddingModel.Id, chunk.PageNumber, chunk.TokenCount));
                    db.ChunkEmbeddings.Add(new ChunkEmbedding
                    {
                        ChunkId = chunk.Id,
                        EmbeddingModelId = embeddingModel.Id,
                        VectorCollection = collection,
                        VectorPointId = pointId,
                        Dimension = embedding.Dim,
                        Status = ChunkEmbeddingStatus.Indexed,
                        IndexedAtUtc = clock.UtcNow,
                    });
                }
            }

            await AdvanceStageAsync(job, ProcessingStage.Index, ct);
            await vectors.UpsertChunksAsync(collection, chunkVectors, ct);
            await db.SaveChangesAsync(ct);

            document.Status = DocumentStatus.Indexed;
            document.IndexedAtUtc = clock.UtcNow;
            job.Stage = ProcessingStage.Complete;
            job.State = ProcessingState.Succeeded;
            job.FinishedAtUtc = clock.UtcNow;
            job.UpdatedAtUtc = clock.UtcNow;
            job.Error = null;
            await db.SaveChangesAsync(ct);

            logger.LogInformation("Ingested document {DocumentId}: {Chunks} chunks into {Collection}.",
                documentId, chunkEntities.Count, collection);
        }
        catch (AiServiceException ex) when (ex.IsPermanent)
        {
            // Permanent data error (e.g. corrupt/invalid file): mark failed and do NOT rethrow,
            // so Hangfire does not retry a job that can never succeed.
            logger.LogWarning(ex, "Ingestion permanently failed (data error) for document {DocumentId}; not retrying.", documentId);
            document.Status = DocumentStatus.Failed;
            job.State = ProcessingState.Failed;
            job.Error = ex.Message.Length > 1900 ? ex.Message[..1900] : ex.Message;
            job.FinishedAtUtc = clock.UtcNow;
            job.UpdatedAtUtc = clock.UtcNow;
            await db.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ingestion failed for document {DocumentId}.", documentId);
            document.Status = DocumentStatus.Failed;
            job.State = ProcessingState.Failed;
            job.Error = ex.Message.Length > 1900 ? ex.Message[..1900] : ex.Message;
            job.FinishedAtUtc = clock.UtcNow;
            job.UpdatedAtUtc = clock.UtcNow;
            await db.SaveChangesAsync(ct);
            throw;
        }
    }

    private async Task AdvanceStageAsync(DocumentProcessingJob job, ProcessingStage stage, CancellationToken ct)
    {
        job.Stage = stage;
        job.UpdatedAtUtc = clock.UtcNow;
        await db.SaveChangesAsync(ct);
    }
}
