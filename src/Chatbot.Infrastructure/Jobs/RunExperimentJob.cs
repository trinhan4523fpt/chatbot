using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using Chatbot.Application.Common;
using Chatbot.Application.Common.Interfaces;
using Chatbot.Domain.Entities;
using Chatbot.Domain.Enums;
using Chatbot.Infrastructure.Options;
using Chatbot.Infrastructure.Persistence;
using Chatbot.Infrastructure.Vectors;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Chatbot.Infrastructure.Jobs;

/// <summary>
/// Runs one benchmark configuration over the subject's test set: generate answers via the RAG
/// pipeline (run's embedding/strategy/llm), score with RAGAS (Python), aggregate. Idempotent per
/// question (PerQuestionStatus) and reproducible (corpus snapshot).
/// </summary>
public sealed class RunExperimentJob(
    ChatbotDbContext db,
    IAiServiceClient ai,
    IVectorStore vectors,
    IChatCompletionService chat,
    IOptions<OllamaOptions> ollamaOptions,
    IClock clock,
    IFileStorageService storage,
    ILogger<RunExperimentJob> logger)
{
    private const string SystemInstruction =
        "Bạn là trợ lý học tập của một trường đại học Việt Nam. " +
        "QUY TẮC NGÔN NGỮ (BẮT BUỘC, không có ngoại lệ): toàn bộ câu trả lời PHẢI viết 100% bằng tiếng Việt. " +
        "TUYỆT ĐỐI KHÔNG được dùng tiếng Trung, chữ Hán, tiếng Anh hay bất kỳ ngôn ngữ nào khác. " +
        "Không chèn chữ Hán vào giữa câu tiếng Việt. Nếu tài liệu tham khảo chứa ngôn ngữ khác, hãy dịch sang tiếng Việt. " +
        "Chỉ trả lời dựa trên [NỘI DUNG THAM KHẢO] được cung cấp. " +
        "Nếu thông tin không có trong tài liệu, trả lời đúng câu: \"Tôi không tìm thấy thông tin này trong tài liệu.\" " +
        "Trả lời ngắn gọn.";

    private const string LanguageReminder =
        "Nhắc lại: trả lời hoàn toàn bằng tiếng Việt, không dùng chữ Hán hay tiếng Trung.";

    [AutomaticRetry(Attempts = 2)]
    public async Task RunAsync(long experimentRunId, CancellationToken ct)
    {
        var run = await db.ExperimentRuns.Include(r => r.Experiment).FirstOrDefaultAsync(r => r.Id == experimentRunId, ct);
        if (run is null)
        {
            return;
        }

        var cfg = await db.SystemConfigurations.AsNoTracking().FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("System configuration is missing.");
        var subjectId = run.Experiment.SubjectId
            ?? throw new InvalidOperationException("Experiment has no subject.");

        var embeddingModel = await db.EmbeddingModels.FirstAsync(
            m => m.Id == (run.EmbeddingModelId ?? cfg.ActiveEmbeddingModelId), ct);
        var strategyId = run.ChunkingStrategyId ?? cfg.ActiveChunkingStrategyId!.Value;
        var llm = await db.LlmModels.FirstAsync(m => m.Id == (run.LlmModelId ?? cfg.ActiveLlmModelId), ct);
        var collection = VectorCollectionNaming.For(embeddingModel.QdrantCollectionName, strategyId);

        var docIds = await db.Documents
            .Where(d => d.SubjectId == subjectId && d.Status == DocumentStatus.Indexed)
            .Select(d => d.Id).ToListAsync(ct);
        run.Status = RunStatus.Running;
        run.StartedAtUtc ??= clock.UtcNow;
        run.CorpusSnapshot = JsonSerializer.Serialize(new
        {
            subjectId, documentIds = docIds, collection,
            embeddingModel = embeddingModel.Name, chunkingStrategy = strategyId, llmModel = llm.Name,
        });
        await db.SaveChangesAsync(ct);

        try
        {
            await EnsureCorpusIndexedAsync(subjectId, embeddingModel, strategyId, ct);

            var questions = await db.TestQuestions.Where(q => q.SubjectId == subjectId).OrderBy(q => q.Id).ToListAsync(ct);
            var existingResults = await db.EvaluationResults
                .Include(r => r.Retrievals)
                .Where(r => r.ExperimentRunId == run.Id)
                .ToListAsync(ct);
            var resultsByQuestion = existingResults.ToDictionary(r => r.TestQuestionId);

            foreach (var question in questions)
            {
                if (resultsByQuestion.TryGetValue(question.Id, out var done) && done.PerQuestionStatus == PerQuestionStatus.Done)
                {
                    continue; // idempotent: never re-run completed questions
                }

                var result = done ?? new EvaluationResult { ExperimentRunId = run.Id, TestQuestionId = question.Id };
                if (done is null)
                {
                    db.EvaluationResults.Add(result);
                }
                else if (result.Retrievals.Count > 0)
                {
                    db.EvaluationRetrievals.RemoveRange(result.Retrievals);
                }

                try
                {
                    var stopwatch = Stopwatch.StartNew();
                    var embedding = await ai.EmbedAsync([question.Question], embeddingModel.Name, "query", ct);
                    var hits = await vectors.SearchAsync(collection, embedding.Vectors[0], cfg.RetrievalTopK, subjectId, ct);

                    var contexts = new List<string>();
                    if (hits.Count > 0)
                    {
                        var chunkIds = hits.Select(h => h.ChunkId).ToList();
                        var chunks = await db.DocumentChunks.AsNoTracking()
                            .Where(c => chunkIds.Contains(c.Id))
                            .Select(c => new { c.Id, c.Content, c.DocumentId })
                            .ToListAsync(ct);
                        var byId = chunks.ToDictionary(x => x.Id);
                        var rank = 1;
                        foreach (var hit in hits)
                        {
                            if (!byId.TryGetValue(hit.ChunkId, out var chunk))
                            {
                                continue;
                            }

                            contexts.Add(chunk.Content);
                            result.Retrievals.Add(new EvaluationRetrieval
                            {
                                ChunkId = chunk.Id, DocumentId = chunk.DocumentId, Rank = rank,
                                Score = (decimal)Math.Round(hit.Score, 6),
                                Snippet = chunk.Content.Length > 300 ? chunk.Content[..300] : chunk.Content,
                            });
                            rank++;
                        }
                    }

                    result.GeneratedAnswer = await GenerateAsync(contexts, question.Question, llm.Name, cfg.PromptTemplate, ct);
                    result.RetrievedContexts = JsonSerializer.Serialize(contexts);
                    result.LatencyMs = (int)stopwatch.ElapsedMilliseconds;
                    result.PerQuestionStatus = PerQuestionStatus.Done;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    result.PerQuestionStatus = PerQuestionStatus.Error;
                    logger.LogWarning(ex, "Benchmark generation failed for run {Run} question {Question}.", run.Id, question.Id);
                }

                await db.SaveChangesAsync(ct);
            }

            await ScoreWithRagasAsync(run.Id, ct);
            await UpsertMetricAsync(run.Id, ct);

            run.Status = RunStatus.Done;
            run.FinishedAtUtc = clock.UtcNow;
            await db.SaveChangesAsync(ct);
            await MaybeCompleteExperimentAsync(run.ExperimentId, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Experiment run {Run} failed.", run.Id);
            run.Status = RunStatus.Error;
            run.FinishedAtUtc = clock.UtcNow;
            await db.SaveChangesAsync(CancellationToken.None);
            throw;
        }
    }

    private async Task<string> GenerateAsync(
        List<string> contexts, string question, string llmModel, string? template, CancellationToken ct)
    {
        if (contexts.Count == 0)
        {
            return "Tôi không tìm thấy thông tin này trong tài liệu.";
        }

        var contextBuilder = new StringBuilder();
        for (var i = 0; i < contexts.Count; i++)
        {
            contextBuilder.AppendLine($"[Nguồn {i + 1}] {contexts[i]}");
        }

        var prompt = (template ?? "[NỘI DUNG THAM KHẢO]\n{context}\n\n[CÂU HỎI]\n{question}")
            .Replace("{context}", contextBuilder.ToString())
            .Replace("{question}", question)
            + "\n\n" + LanguageReminder;

        var turns = new List<ChatTurn> { new("system", SystemInstruction), new("user", prompt) };
        var answer = new StringBuilder();
        // Benchmarks use the model's default sampling so runs stay comparable.
        await foreach (var delta in chat.StreamAsync(turns, llmModel, options: null, ct))
        {
            answer.Append(delta);
        }

        return answer.ToString();
    }

    private async Task ScoreWithRagasAsync(long runId, CancellationToken ct)
    {
        var done = await db.EvaluationResults
            .Include(r => r.TestQuestion)
            .Where(r => r.ExperimentRunId == runId && r.PerQuestionStatus == PerQuestionStatus.Done)
            .OrderBy(r => r.Id)
            .ToListAsync(ct);
        if (done.Count == 0)
        {
            return;
        }

        var items = done.Select(r => new RagEvalItem(
            r.TestQuestion.Question,
            r.GeneratedAnswer ?? string.Empty,
            DeserializeContexts(r.RetrievedContexts),
            r.TestQuestion.GroundTruth,
            r.TestQuestion.ReferenceContext)).ToList();

        var eval = await ai.RagEvalAsync(items, ollamaOptions.Value.JudgeModel, ct);
        for (var i = 0; i < done.Count && i < eval.PerItem.Count; i++)
        {
            var s = eval.PerItem[i].Scores;
            var r = done[i];
            r.Faithfulness = ToScore(s.Faithfulness);
            r.AnswerRelevancy = ToScore(s.AnswerRelevancy);
            r.ContextPrecision = ToScore(s.ContextPrecision);
            r.ContextRecall = ToScore(s.ContextRecall);
            r.AnswerCorrectness = ToScore(s.AnswerCorrectness);
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task UpsertMetricAsync(long runId, CancellationToken ct)
    {
        var agg = await db.EvaluationResults
            .Where(r => r.ExperimentRunId == runId && r.PerQuestionStatus == PerQuestionStatus.Done)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Faith = g.Average(x => x.Faithfulness),
                AnsRel = g.Average(x => x.AnswerRelevancy),
                CtxPrec = g.Average(x => x.ContextPrecision),
                CtxRec = g.Average(x => x.ContextRecall),
                AnsCorr = g.Average(x => x.AnswerCorrectness),
                Lat = g.Average(x => (decimal?)x.LatencyMs),
                Total = g.Count(),
            })
            .FirstOrDefaultAsync(ct);

        var metric = await db.ExperimentRunMetrics.FirstOrDefaultAsync(m => m.ExperimentRunId == runId, ct);
        if (metric is null)
        {
            metric = new ExperimentRunMetric { ExperimentRunId = runId };
            db.ExperimentRunMetrics.Add(metric);
        }

        metric.AvgFaithfulness = agg?.Faith;
        metric.AvgAnswerRelevancy = agg?.AnsRel;
        metric.AvgContextPrecision = agg?.CtxPrec;
        metric.AvgContextRecall = agg?.CtxRec;
        metric.AvgAnswerCorrectness = agg?.AnsCorr;
        metric.AvgLatencyMs = agg?.Lat is { } lat ? Math.Round(lat, 2) : null;
        metric.TotalQuestions = agg?.Total ?? 0;
        metric.ComputedAtUtc = clock.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private async Task MaybeCompleteExperimentAsync(long experimentId, CancellationToken ct)
    {
        var anyOpen = await db.ExperimentRuns.AnyAsync(
            r => r.ExperimentId == experimentId &&
                 (r.Status == RunStatus.Queued || r.Status == RunStatus.Running), ct);
        if (!anyOpen)
        {
            var experiment = await db.Experiments.FirstOrDefaultAsync(e => e.Id == experimentId, ct);
            if (experiment is not null)
            {
                experiment.Status = ExperimentStatus.Done;
                await db.SaveChangesAsync(ct);
            }
        }
    }

    private static List<string> DeserializeContexts(string? json) =>
        string.IsNullOrWhiteSpace(json)
            ? []
            : JsonSerializer.Deserialize<List<string>>(json) ?? [];

    private static decimal? ToScore(double? value) =>
        value is { } v ? (decimal)Math.Round(Math.Clamp(v, 0, 1), 6) : null;

    private async Task EnsureCorpusIndexedAsync(
        long subjectId,
        EmbeddingModel embeddingModel,
        long strategyId,
        CancellationToken ct)
    {
        var docs = await db.Documents
            .Where(d => d.SubjectId == subjectId && d.Status == DocumentStatus.Indexed)
            .ToListAsync(ct);

        foreach (var doc in docs)
        {
            var chunksCount = await db.DocumentChunks
                .CountAsync(c => c.DocumentId == doc.Id && c.ChunkingStrategyId == strategyId, ct);

            var indexedEmbeddingsCount = await db.DocumentChunks
                .Where(c => c.DocumentId == doc.Id && c.ChunkingStrategyId == strategyId)
                .SelectMany(c => c.Embeddings)
                .CountAsync(e => e.EmbeddingModelId == embeddingModel.Id && e.Status == ChunkEmbeddingStatus.Indexed, ct);

            if (chunksCount == 0 || chunksCount != indexedEmbeddingsCount)
            {
                logger.LogInformation("Dynamically indexing document {DocumentId} ({DocumentTitle}) for strategy {StrategyId} and embedding model {EmbeddingModelId}.", 
                    doc.Id, doc.Title, strategyId, embeddingModel.Id);

                var physicalPath = storage.ResolvePhysicalPath(doc.RelativePath);
                var bytes = await File.ReadAllBytesAsync(physicalPath, ct);

                var parsed = await ai.ParseAsync(bytes, doc.OriginalFileName, ct);

                var strategy = await db.ChunkingStrategies.FirstAsync(s => s.Id == strategyId, ct);
                var chunks = await ai.ChunkAsync(
                    parsed.Pages, strategy.Name, strategy.ChunkSize ?? 512, strategy.ChunkOverlap ?? 50, ct);

                if (chunks.Count == 0)
                {
                    logger.LogWarning("No chunks extracted from document {DocumentId} during dynamic indexing.", doc.Id);
                    continue;
                }

                var existingChunks = await db.DocumentChunks
                    .Where(c => c.DocumentId == doc.Id && c.ChunkingStrategyId == strategyId)
                    .ToListAsync(ct);
                db.DocumentChunks.RemoveRange(existingChunks);
                await db.SaveChangesAsync(ct);

                var chunkEntities = chunks
                    .Select(c => new DocumentChunk
                    {
                        DocumentId = doc.Id,
                        ChunkingStrategyId = strategyId,
                        ChunkIndex = c.Index,
                        Content = c.Content,
                        PageNumber = c.Page,
                        TokenCount = c.TokenCount,
                    })
                    .ToList();
                db.DocumentChunks.AddRange(chunkEntities);
                await db.SaveChangesAsync(ct);

                var collectionName = VectorCollectionNaming.For(embeddingModel.QdrantCollectionName, strategyId);
                await vectors.EnsureCollectionAsync(collectionName, embeddingModel.Dimension, ct);
                await vectors.DeleteByDocumentAsync(collectionName, doc.Id, ct);

                var chunkVectors = new List<ChunkVector>(chunkEntities.Count);
                const int EmbedBatchSize = 64;

                foreach (var batch in chunkEntities.Chunk(EmbedBatchSize))
                {
                    var embeddingResult = await ai.EmbedAsync(
                        batch.Select(c => c.Content).ToList(), embeddingModel.Name, "passage", ct);
                    if (embeddingResult.Dim != embeddingModel.Dimension)
                    {
                        throw new InvalidOperationException(
                            $"Embedding dimension mismatch for '{embeddingModel.Name}': expected {embeddingModel.Dimension}, got {embeddingResult.Dim}.");
                    }

                    for (var i = 0; i < batch.Length; i++)
                    {
                        var chunk = batch[i];
                        var pointId = PointIds.For(chunk.Id, embeddingModel.Id);
                        chunkVectors.Add(new ChunkVector(
                            pointId, embeddingResult.Vectors[i], chunk.Id, doc.Id, doc.SubjectId,
                            doc.ChapterId, strategyId, embeddingModel.Id, chunk.PageNumber, chunk.TokenCount));
                        db.ChunkEmbeddings.Add(new ChunkEmbedding
                        {
                            ChunkId = chunk.Id,
                            EmbeddingModelId = embeddingModel.Id,
                            VectorCollection = collectionName,
                            VectorPointId = pointId,
                            Dimension = embeddingResult.Dim,
                            Status = ChunkEmbeddingStatus.Indexed,
                            IndexedAtUtc = clock.UtcNow,
                        });
                    }
                }

                await vectors.UpsertChunksAsync(collectionName, chunkVectors, ct);
                await db.SaveChangesAsync(ct);

                logger.LogInformation("Successfully dynamically indexed document {DocumentId} into collection {CollectionName}.", 
                    doc.Id, collectionName);
            }
        }
    }
}
