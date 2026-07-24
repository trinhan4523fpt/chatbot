using Chatbot.Application.Common.Exceptions;
using Chatbot.Application.Common.Interfaces;
using Chatbot.Domain.Entities;
using Chatbot.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Chatbot.Application.Features.Admin.Configuration;

// ---- Read ----------------------------------------------------------------------

/// <summary>The active RAG configuration, plus how much of the corpus still matches it.</summary>
public sealed record SystemConfigurationDto(
    long? ActiveEmbeddingModelId,
    string? ActiveEmbeddingModelName,
    long? ActiveChunkingStrategyId,
    string? ActiveChunkingStrategyName,
    long? ActiveLlmModelId,
    string? ActiveLlmModelName,
    int RetrievalTopK,
    decimal MinRelevanceScore,
    bool ScopeRestriction,
    string? PromptTemplate,
    int HistoryWindowTurns,
    long MaxUploadBytes,
    CorpusStatusDto Corpus);

/// <summary>
/// Whether indexed documents still match the active embedding model + chunking strategy.
/// Retrieval only finds a document when it has been indexed for the *current* pair, so a
/// stale count means those documents are invisible to chat until they are reindexed.
/// </summary>
public sealed record CorpusStatusDto(int IndexedDocuments, int UpToDate, int Stale, bool NeedsReindex);

public sealed record GetSystemConfigurationQuery : IRequest<SystemConfigurationDto>;

public sealed class GetSystemConfigurationQueryHandler(IAppDbContext db)
    : IRequestHandler<GetSystemConfigurationQuery, SystemConfigurationDto>
{
    public async Task<SystemConfigurationDto> Handle(GetSystemConfigurationQuery request, CancellationToken ct)
    {
        var cfg = await db.SystemConfigurations.AsNoTracking().FirstOrDefaultAsync(c => c.Id == 1, ct)
            ?? throw new NotFoundException("Chưa có cấu hình hệ thống.");

        var embedding = cfg.ActiveEmbeddingModelId is null
            ? null
            : await db.EmbeddingModels.AsNoTracking().FirstOrDefaultAsync(m => m.Id == cfg.ActiveEmbeddingModelId, ct);
        var strategy = cfg.ActiveChunkingStrategyId is null
            ? null
            : await db.ChunkingStrategies.AsNoTracking().FirstOrDefaultAsync(s => s.Id == cfg.ActiveChunkingStrategyId, ct);
        var llm = cfg.ActiveLlmModelId is null
            ? null
            : await db.LlmModels.AsNoTracking().FirstOrDefaultAsync(m => m.Id == cfg.ActiveLlmModelId, ct);

        var corpus = await CorpusStatus.ComputeAsync(
            db, cfg.ActiveEmbeddingModelId, cfg.ActiveChunkingStrategyId, ct);

        return new SystemConfigurationDto(
            cfg.ActiveEmbeddingModelId, embedding?.Name,
            cfg.ActiveChunkingStrategyId, strategy?.Name,
            cfg.ActiveLlmModelId, llm?.Name,
            cfg.RetrievalTopK, cfg.MinRelevanceScore, cfg.ScopeRestriction,
            cfg.PromptTemplate, cfg.HistoryWindowTurns, cfg.MaxUploadBytes,
            corpus);
    }
}

// ---- Options (for the settings UI) ---------------------------------------------

/// <summary>One selectable value, with enough detail to render a meaningful dropdown label.</summary>
public sealed record ConfigOptionDto(long Id, string Name, string? Detail, bool IsActive, bool IsSelected);

/// <summary>A numeric setting's allowed range, so the UI can bound its input without hardcoding.</summary>
public sealed record ConfigRangeDto(string Field, decimal Min, decimal Max, decimal Current);

/// <summary>
/// Everything a settings screen needs in one call: the selectable values for each axis, which one
/// is currently active, the valid ranges for the numeric tunables, and the corpus status.
/// </summary>
public sealed record ConfigOptionsDto(
    IReadOnlyList<ConfigOptionDto> EmbeddingModels,
    IReadOnlyList<ConfigOptionDto> ChunkingStrategies,
    IReadOnlyList<ConfigOptionDto> LlmModels,
    IReadOnlyList<ConfigRangeDto> Ranges,
    CorpusStatusDto Corpus);

public sealed record GetConfigOptionsQuery : IRequest<ConfigOptionsDto>;

public sealed class GetConfigOptionsQueryHandler(IAppDbContext db)
    : IRequestHandler<GetConfigOptionsQuery, ConfigOptionsDto>
{
    public async Task<ConfigOptionsDto> Handle(GetConfigOptionsQuery request, CancellationToken ct)
    {
        var cfg = await db.SystemConfigurations.AsNoTracking().FirstOrDefaultAsync(c => c.Id == 1, ct)
            ?? throw new NotFoundException("Chưa có cấu hình hệ thống.");

        var embeddings = await db.EmbeddingModels.AsNoTracking().OrderBy(m => m.Name)
            .Select(m => new ConfigOptionDto(
                m.Id, m.Name,
                $"{m.Dimension} chiều · {m.Provider}" + (m.IsFree ? "" : " · cần API key"),
                m.IsActive, m.Id == cfg.ActiveEmbeddingModelId))
            .ToListAsync(ct);

        var strategies = await db.ChunkingStrategies.AsNoTracking().OrderBy(s => s.Name)
            .Select(s => new ConfigOptionDto(
                s.Id, s.Name,
                s.ChunkSize == null ? s.Description : $"{s.ChunkSize} token · overlap {s.ChunkOverlap}",
                s.IsActive, s.Id == cfg.ActiveChunkingStrategyId))
            .ToListAsync(ct);

        var llms = await db.LlmModels.AsNoTracking().OrderBy(m => m.Name)
            .Select(m => new ConfigOptionDto(
                m.Id, m.Name, $"{m.Type} · {m.Provider}", m.IsActive, m.Id == cfg.ActiveLlmModelId))
            .ToListAsync(ct);

        // Kept in step with UpdateSystemConfigurationCommandValidator.
        var ranges = new List<ConfigRangeDto>
        {
            new("retrievalTopK", 1, 50, cfg.RetrievalTopK),
            new("minRelevanceScore", 0m, 1m, cfg.MinRelevanceScore),
            new("historyWindowTurns", 0, 50, cfg.HistoryWindowTurns),
        };

        var corpus = await CorpusStatus.ComputeAsync(
            db, cfg.ActiveEmbeddingModelId, cfg.ActiveChunkingStrategyId, ct);

        return new ConfigOptionsDto(embeddings, strategies, llms, ranges, corpus);
    }
}

// ---- Update --------------------------------------------------------------------

/// <summary>
/// Updates the active RAG configuration. Null fields are left unchanged, so a caller can
/// change one setting without resending the rest.
/// </summary>
public sealed record UpdateSystemConfigurationCommand(
    long? ActiveEmbeddingModelId,
    long? ActiveChunkingStrategyId,
    long? ActiveLlmModelId,
    int? RetrievalTopK,
    decimal? MinRelevanceScore,
    bool? ScopeRestriction,
    string? PromptTemplate,
    int? HistoryWindowTurns,
    bool ReindexNow) : IRequest<UpdateSystemConfigurationResult>;

/// <summary>
/// What changed, and what it means for the corpus. <paramref name="ReindexQueued"/> counts the
/// documents actually enqueued; <paramref name="Warning"/> is set when documents were left stale.
/// </summary>
public sealed record UpdateSystemConfigurationResult(
    bool EmbeddingChanged,
    bool ChunkingChanged,
    bool RequiresReindex,
    int ReindexQueued,
    int StaleDocuments,
    string? Warning);

public sealed class UpdateSystemConfigurationCommandValidator : AbstractValidator<UpdateSystemConfigurationCommand>
{
    public UpdateSystemConfigurationCommandValidator()
    {
        RuleFor(x => x.RetrievalTopK).InclusiveBetween(1, 50).When(x => x.RetrievalTopK.HasValue);
        RuleFor(x => x.MinRelevanceScore).InclusiveBetween(0m, 1m).When(x => x.MinRelevanceScore.HasValue);
        RuleFor(x => x.HistoryWindowTurns).InclusiveBetween(0, 50).When(x => x.HistoryWindowTurns.HasValue);
        RuleFor(x => x.PromptTemplate)
            .Must(t => t!.Contains("{context}") && t.Contains("{question}"))
            .WithMessage("PromptTemplate phải chứa cả {context} và {question}.")
            .When(x => !string.IsNullOrWhiteSpace(x.PromptTemplate));
    }
}

public sealed class UpdateSystemConfigurationCommandHandler(
    IAppDbContext db, IJobScheduler jobScheduler, ICurrentUser currentUser)
    : IRequestHandler<UpdateSystemConfigurationCommand, UpdateSystemConfigurationResult>
{
    public async Task<UpdateSystemConfigurationResult> Handle(
        UpdateSystemConfigurationCommand request, CancellationToken ct)
    {
        var cfg = await db.SystemConfigurations.FirstOrDefaultAsync(c => c.Id == 1, ct)
            ?? throw new NotFoundException("Chưa có cấu hình hệ thống.");

        var embeddingChanged = request.ActiveEmbeddingModelId is { } embId && embId != cfg.ActiveEmbeddingModelId;
        var chunkingChanged = request.ActiveChunkingStrategyId is { } stratId && stratId != cfg.ActiveChunkingStrategyId;

        if (request.ActiveEmbeddingModelId is { } newEmbeddingId)
        {
            var model = await db.EmbeddingModels.FirstOrDefaultAsync(m => m.Id == newEmbeddingId, ct)
                ?? throw new NotFoundException("Không tìm thấy embedding model.");
            if (!model.IsActive)
            {
                throw new BusinessRuleException($"Embedding model '{model.Name}' đang bị tắt.");
            }

            cfg.ActiveEmbeddingModelId = newEmbeddingId;
        }

        if (request.ActiveChunkingStrategyId is { } newStrategyId)
        {
            var strategy = await db.ChunkingStrategies.FirstOrDefaultAsync(s => s.Id == newStrategyId, ct)
                ?? throw new NotFoundException("Không tìm thấy chunking strategy.");
            if (!strategy.IsActive)
            {
                throw new BusinessRuleException($"Chunking strategy '{strategy.Name}' đang bị tắt.");
            }

            cfg.ActiveChunkingStrategyId = newStrategyId;
        }

        if (request.ActiveLlmModelId is { } newLlmId)
        {
            var llm = await db.LlmModels.FirstOrDefaultAsync(m => m.Id == newLlmId, ct)
                ?? throw new NotFoundException("Không tìm thấy LLM model.");
            if (!llm.IsActive)
            {
                throw new BusinessRuleException($"LLM '{llm.Name}' đang bị tắt.");
            }

            cfg.ActiveLlmModelId = newLlmId;
        }

        if (request.RetrievalTopK is { } topK)
        {
            cfg.RetrievalTopK = topK;
        }

        if (request.MinRelevanceScore is { } minScore)
        {
            cfg.MinRelevanceScore = minScore;
        }

        if (request.ScopeRestriction is { } scope)
        {
            cfg.ScopeRestriction = scope;
        }

        if (request.HistoryWindowTurns is { } history)
        {
            cfg.HistoryWindowTurns = history;
        }

        if (!string.IsNullOrWhiteSpace(request.PromptTemplate))
        {
            cfg.PromptTemplate = request.PromptTemplate;
        }

        db.AuditLogs.Add(new AuditLog
        {
            Action = "SystemConfigurationUpdated",
            ActorUserId = currentUser.UserId,
            ActorEmail = currentUser.Email,
            TargetType = "SystemConfiguration",
            TargetId = "1",
        });
        await db.SaveChangesAsync(ct);

        // Retrieval is keyed on (embedding model, chunking strategy). Changing either leaves every
        // already-indexed document unreachable until it is reindexed for the new pair.
        var requiresReindex = embeddingChanged || chunkingChanged;
        var queued = 0;
        string? warning = null;

        var corpus = await CorpusStatus.ComputeAsync(
            db, cfg.ActiveEmbeddingModelId, cfg.ActiveChunkingStrategyId, ct);

        if (requiresReindex && corpus.Stale > 0)
        {
            if (request.ReindexNow)
            {
                queued = await QueueReindexAsync(ct);
                warning = $"Đã xếp hàng index lại {queued} tài liệu. " +
                          "Trong lúc chờ, các tài liệu này chưa trả lời được.";
            }
            else
            {
                warning = $"{corpus.Stale} tài liệu chưa khớp cấu hình mới nên chatbot KHÔNG tìm thấy. " +
                          "Gọi lại với reindexNow=true, hoặc POST /api/v1/admin/config/reindex.";
            }
        }

        return new UpdateSystemConfigurationResult(
            embeddingChanged, chunkingChanged, requiresReindex, queued, corpus.Stale, warning);
    }

    private async Task<int> QueueReindexAsync(CancellationToken ct) =>
        await ReindexCorpus.QueueAsync(db, jobScheduler, currentUser, ct);
}

// ---- Reindex the whole corpus --------------------------------------------------

public sealed record ReindexCorpusCommand : IRequest<int>;

public sealed class ReindexCorpusCommandHandler(
    IAppDbContext db, IJobScheduler jobScheduler, ICurrentUser currentUser)
    : IRequestHandler<ReindexCorpusCommand, int>
{
    public async Task<int> Handle(ReindexCorpusCommand request, CancellationToken ct) =>
        await ReindexCorpus.QueueAsync(db, jobScheduler, currentUser, ct);
}

// ---- Shared helpers ------------------------------------------------------------

internal static class CorpusStatus
{
    /// <summary>
    /// Counts indexed documents that already have chunks for <paramref name="strategyId"/> whose
    /// embeddings exist for <paramref name="embeddingModelId"/>. Anything else is stale, i.e.
    /// present in the database but not retrievable under the current configuration.
    /// </summary>
    public static async Task<CorpusStatusDto> ComputeAsync(
        IAppDbContext db, long? embeddingModelId, long? strategyId, CancellationToken ct)
    {
        var indexed = await db.Documents
            .Where(d => d.Status == DocumentStatus.Indexed)
            .Select(d => d.Id)
            .ToListAsync(ct);

        if (indexed.Count == 0 || embeddingModelId is null || strategyId is null)
        {
            return new CorpusStatusDto(indexed.Count, 0, indexed.Count, indexed.Count > 0);
        }

        var upToDate = await db.DocumentChunks
            .Where(c => indexed.Contains(c.DocumentId) && c.ChunkingStrategyId == strategyId)
            .Where(c => c.Embeddings.Any(e =>
                e.EmbeddingModelId == embeddingModelId && e.Status == ChunkEmbeddingStatus.Indexed))
            .Select(c => c.DocumentId)
            .Distinct()
            .CountAsync(ct);

        var stale = indexed.Count - upToDate;
        return new CorpusStatusDto(indexed.Count, upToDate, stale, stale > 0);
    }
}

internal static class ReindexCorpus
{
    /// <summary>
    /// Enqueues an ingest job for every indexed document that has no job already running, so
    /// calling this twice in a row does not double-queue the same document.
    /// </summary>
    public static async Task<int> QueueAsync(
        IAppDbContext db, IJobScheduler jobScheduler, ICurrentUser currentUser, CancellationToken ct)
    {
        var busy = await db.DocumentProcessingJobs
            .Where(j => j.State == ProcessingState.Queued || j.State == ProcessingState.Running)
            .Select(j => j.DocumentId)
            .Distinct()
            .ToListAsync(ct);

        var documents = await db.Documents
            .Where(d => d.Status == DocumentStatus.Indexed && !busy.Contains(d.Id))
            .ToListAsync(ct);

        if (documents.Count == 0)
        {
            return 0;
        }

        var jobs = new List<DocumentProcessingJob>(documents.Count);
        foreach (var document in documents)
        {
            document.Status = DocumentStatus.Uploaded;
            var job = new DocumentProcessingJob
            {
                DocumentId = document.Id,
                State = ProcessingState.Queued,
                Stage = ProcessingStage.Parse,
            };
            jobs.Add(job);
            db.DocumentProcessingJobs.Add(job);
        }

        db.AuditLogs.Add(new AuditLog
        {
            Action = "CorpusReindexRequested",
            ActorUserId = currentUser.UserId,
            ActorEmail = currentUser.Email,
            TargetType = "SystemConfiguration",
            TargetId = "1",
            NewValues = $"{{\"documents\":{documents.Count}}}",
        });
        await db.SaveChangesAsync(ct);

        foreach (var job in jobs)
        {
            job.HangfireJobId = jobScheduler.EnqueueIngest(job.DocumentId);
        }

        await db.SaveChangesAsync(ct);
        return documents.Count;
    }
}
