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
    int? ActiveChunkSize,
    int? ActiveChunkOverlap,
    long? ActiveLlmModelId,
    string? ActiveLlmModelName,
    int RetrievalTopK,
    decimal MinRelevanceScore,
    bool ScopeRestriction,
    string? PromptTemplate,
    int HistoryWindowTurns,
    decimal Temperature,
    int? MaxOutputTokens,
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
            cfg.ActiveChunkSize, cfg.ActiveChunkOverlap,
            cfg.ActiveLlmModelId, llm?.Name,
            cfg.RetrievalTopK, cfg.MinRelevanceScore, cfg.ScopeRestriction,
            cfg.PromptTemplate, cfg.HistoryWindowTurns,
            cfg.Temperature, cfg.MaxOutputTokens, cfg.MaxUploadBytes,
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
            new("activeChunkSize", 50, 4096, cfg.ActiveChunkSize ?? 0),
            new("activeChunkOverlap", 0, 1024, cfg.ActiveChunkOverlap ?? 0),
            new("retrievalTopK", 1, 50, cfg.RetrievalTopK),
            new("minRelevanceScore", 0m, 1m, cfg.MinRelevanceScore),
            new("historyWindowTurns", 0, 50, cfg.HistoryWindowTurns),
            new("temperature", 0m, 2m, cfg.Temperature),
            new("maxOutputTokens", 1, 8192, cfg.MaxOutputTokens ?? 0),
        };

        var corpus = await CorpusStatus.ComputeAsync(
            db, cfg.ActiveEmbeddingModelId, cfg.ActiveChunkingStrategyId, ct);

        return new ConfigOptionsDto(embeddings, strategies, llms, ranges, corpus);
    }
}

// ---- UI schema -----------------------------------------------------------------

/// <summary>
/// Describes the settings UI as tabs of fields, so the frontend can render a Dify-style config
/// screen without hardcoding it. Only fields the backend actually honours are listed; planned
/// features (hybrid search, reranker, cache...) are intentionally absent, not shown as disabled.
/// </summary>
public sealed record ConfigSchemaDto(IReadOnlyList<ConfigTabDto> Tabs);

/// <summary>A tab in the settings UI. <paramref name="Advanced"/> tabs can be collapsed by default.</summary>
public sealed record ConfigTabDto(string Key, string Title, bool Advanced, IReadOnlyList<ConfigFieldDto> Fields);

/// <summary>
/// One editable field. <paramref name="Type"/> drives the control (number/decimal/bool/text/select);
/// <paramref name="Min"/>/<paramref name="Max"/> bound numbers; <paramref name="RequiresReindex"/>
/// flags a change that makes the corpus stale.
/// </summary>
public sealed record ConfigFieldDto(
    string Key,
    string Label,
    string Type,
    string? Help,
    decimal? Min,
    decimal? Max,
    bool RequiresReindex);

public sealed record GetConfigSchemaQuery : IRequest<ConfigSchemaDto>;

public sealed class GetConfigSchemaQueryHandler : IRequestHandler<GetConfigSchemaQuery, ConfigSchemaDto>
{
    // Static: the shape of the settings screen does not depend on data, only the current values do
    // (those come from GET /config and /config/options).
    private static readonly ConfigSchemaDto Schema = new(new[]
    {
        new ConfigTabDto("knowledge", "Tài liệu & Chunking", false, new[]
        {
            new ConfigFieldDto("activeChunkingStrategyId", "Chiến lược chunking", "select",
                "Thuật toán cắt (fixed / recursive / semantic...). Đổi sẽ cần index lại.", null, null, true),
            new ConfigFieldDto("activeChunkSize", "Kích thước đoạn (token)", "number",
                "Ghi đè kích thước của chiến lược. Bỏ trống = dùng mặc định. Đổi cần index lại.", 50, 4096, true),
            new ConfigFieldDto("activeChunkOverlap", "Độ chồng lấn (token)", "number",
                "Số token gối đầu giữa 2 đoạn. Phải nhỏ hơn kích thước đoạn. Đổi cần index lại.", 0, 1024, true),
        }),
        new ConfigTabDto("embedding", "Embedding", false, new[]
        {
            new ConfigFieldDto("activeEmbeddingModelId", "Embedding model", "select",
                "Model vector hoá. Đổi sẽ cần index lại (số chiều khác nhau).", null, null, true),
        }),
        new ConfigTabDto("retrieval", "Truy hồi", false, new[]
        {
            new ConfigFieldDto("retrievalTopK", "Số đoạn lấy về (Top K)", "number",
                "Lấy bao nhiêu đoạn gần nhất cho mỗi câu hỏi.", 1, 50, false),
            new ConfigFieldDto("minRelevanceScore", "Ngưỡng liên quan", "decimal",
                "Bỏ đoạn có điểm thấp hơn ngưỡng này (0–1).", 0m, 1m, false),
            new ConfigFieldDto("scopeRestriction", "Chỉ trả lời trong tài liệu", "bool",
                "Bật: không có đoạn nào vượt ngưỡng thì từ chối trả lời.", null, null, false),
        }),
        new ConfigTabDto("generation", "Sinh câu trả lời", false, new[]
        {
            new ConfigFieldDto("activeLlmModelId", "Mô hình LLM", "select",
                "Model sinh câu trả lời. Đổi không cần index lại.", null, null, false),
            new ConfigFieldDto("temperature", "Temperature", "decimal",
                "0 = ổn định, cao = sáng tạo hơn (0–2).", 0m, 2m, false),
            new ConfigFieldDto("maxOutputTokens", "Độ dài tối đa", "number",
                "Số token tối đa mỗi câu trả lời. Bỏ trống = mặc định model.", 1, 8192, false),
            new ConfigFieldDto("historyWindowTurns", "Số lượt hội thoại nhớ", "number",
                "Số lượt hỏi-đáp gần nhất đưa vào ngữ cảnh (0–50).", 0, 50, false),
            new ConfigFieldDto("promptTemplate", "Mẫu prompt", "text",
                "Phải chứa {context} và {question}.", null, null, false),
        }),
    });

    public Task<ConfigSchemaDto> Handle(GetConfigSchemaQuery request, CancellationToken ct) =>
        Task.FromResult(Schema);
}

// ---- Update --------------------------------------------------------------------

/// <summary>
/// Updates the active RAG configuration. Null fields are left unchanged, so a caller can
/// change one setting without resending the rest.
/// </summary>
public sealed record UpdateSystemConfigurationCommand(
    long? ActiveEmbeddingModelId,
    long? ActiveChunkingStrategyId,
    int? ActiveChunkSize,
    int? ActiveChunkOverlap,
    long? ActiveLlmModelId,
    int? RetrievalTopK,
    decimal? MinRelevanceScore,
    bool? ScopeRestriction,
    string? PromptTemplate,
    int? HistoryWindowTurns,
    decimal? Temperature,
    int? MaxOutputTokens,
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
        RuleFor(x => x.ActiveChunkSize).InclusiveBetween(50, 4096).When(x => x.ActiveChunkSize.HasValue);
        RuleFor(x => x.ActiveChunkOverlap).GreaterThanOrEqualTo(0).When(x => x.ActiveChunkOverlap.HasValue);
        RuleFor(x => x)
            .Must(x => x.ActiveChunkOverlap < x.ActiveChunkSize)
            .WithMessage("ChunkOverlap phải nhỏ hơn ChunkSize.")
            .When(x => x.ActiveChunkSize.HasValue && x.ActiveChunkOverlap.HasValue);
        RuleFor(x => x.Temperature).InclusiveBetween(0m, 2m).When(x => x.Temperature.HasValue);
        RuleFor(x => x.MaxOutputTokens).InclusiveBetween(1, 8192).When(x => x.MaxOutputTokens.HasValue);
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
        var strategyChanged = request.ActiveChunkingStrategyId is { } stratId && stratId != cfg.ActiveChunkingStrategyId;
        // Size/overlap changes re-cut every document, but the corpus check keys on strategy id only,
        // so it cannot see them. Track them here to force the stale check below.
        var sizeChanged = request.ActiveChunkSize is { } size && size != cfg.ActiveChunkSize;
        var overlapChanged = request.ActiveChunkOverlap is { } overlap && overlap != cfg.ActiveChunkOverlap;
        var chunkingChanged = strategyChanged || sizeChanged || overlapChanged;

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

        if (request.ActiveChunkSize is { } newChunkSize)
        {
            cfg.ActiveChunkSize = newChunkSize;
        }

        if (request.ActiveChunkOverlap is { } newChunkOverlap)
        {
            cfg.ActiveChunkOverlap = newChunkOverlap;
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

        if (request.Temperature is { } temperature)
        {
            cfg.Temperature = temperature;
        }

        if (request.MaxOutputTokens is { } maxTokens)
        {
            cfg.MaxOutputTokens = maxTokens;
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

        // A size/overlap change re-cuts documents without changing the strategy id, so the corpus
        // check (keyed on strategy id) reports 0 stale. Fall back to the indexed count in that case.
        var stale = (sizeChanged || overlapChanged) && corpus.Stale == 0
            ? corpus.IndexedDocuments
            : corpus.Stale;

        if (requiresReindex && stale > 0)
        {
            if (request.ReindexNow)
            {
                queued = await QueueReindexAsync(ct);
                warning = $"Đã xếp hàng index lại {queued} tài liệu. " +
                          "Trong lúc chờ, các tài liệu này chưa trả lời được.";
            }
            else
            {
                warning = $"{stale} tài liệu chưa khớp cấu hình mới nên chatbot KHÔNG tìm thấy. " +
                          "Gọi lại với reindexNow=true, hoặc POST /api/v1/admin/config/reindex.";
            }
        }

        return new UpdateSystemConfigurationResult(
            embeddingChanged, chunkingChanged, requiresReindex, queued, stale, warning);
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
