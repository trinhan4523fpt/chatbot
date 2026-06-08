using System.Text.Json;
using Chatbot.Application.Common.Exceptions;
using Chatbot.Application.Common.Interfaces;
using Chatbot.Domain.Entities;
using Chatbot.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Chatbot.Application.Features.Experiments;

// ---- Create experiment ---------------------------------------------------------
public sealed record CreateExperimentCommand(string Name, ExperimentType Type, long SubjectId, string? Description)
    : IRequest<long>;

public sealed class CreateExperimentCommandValidator : AbstractValidator<CreateExperimentCommand>
{
    public CreateExperimentCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
        RuleFor(x => x.SubjectId).GreaterThan(0);
    }
}

public sealed class CreateExperimentCommandHandler(IAppDbContext db) : IRequestHandler<CreateExperimentCommand, long>
{
    public async Task<long> Handle(CreateExperimentCommand request, CancellationToken ct)
    {
        if (!await db.Subjects.AnyAsync(s => s.Id == request.SubjectId, ct))
        {
            throw new NotFoundException("Không tìm thấy môn học.");
        }

        var experiment = new Experiment
        {
            Name = request.Name, Type = request.Type, SubjectId = request.SubjectId,
            Description = request.Description, Status = ExperimentStatus.Draft,
        };
        db.Experiments.Add(experiment);
        await db.SaveChangesAsync(ct);
        return experiment.Id;
    }
}

// ---- List / get ----------------------------------------------------------------
public sealed record ListExperimentsQuery : IRequest<IReadOnlyList<ExperimentDto>>;

public sealed class ListExperimentsQueryHandler(IAppDbContext db)
    : IRequestHandler<ListExperimentsQuery, IReadOnlyList<ExperimentDto>>
{
    public async Task<IReadOnlyList<ExperimentDto>> Handle(ListExperimentsQuery request, CancellationToken ct) =>
        await db.Experiments.AsNoTracking().OrderByDescending(e => e.Id)
            .Select(e => new ExperimentDto(e.Id, e.Name, e.Type, e.Status, e.SubjectId, e.Runs.Count, e.CreatedAtUtc))
            .ToListAsync(ct);
}

public sealed record GetExperimentRunsQuery(long ExperimentId) : IRequest<IReadOnlyList<ExperimentRunDto>>;

public sealed class GetExperimentRunsQueryHandler(IAppDbContext db)
    : IRequestHandler<GetExperimentRunsQuery, IReadOnlyList<ExperimentRunDto>>
{
    public async Task<IReadOnlyList<ExperimentRunDto>> Handle(GetExperimentRunsQuery request, CancellationToken ct) =>
        await db.ExperimentRuns.AsNoTracking()
            .Where(r => r.ExperimentId == request.ExperimentId)
            .OrderBy(r => r.Id)
            .Select(r => new ExperimentRunDto(
                r.Id, r.ExperimentId, r.RunName, r.Status,
                r.EmbeddingModelId, r.ChunkingStrategyId, r.LlmModelId, r.StartedAtUtc, r.FinishedAtUtc))
            .ToListAsync(ct);
}

// ---- Create runs (fan-out cross-product) ---------------------------------------
public sealed record CreateRunsCommand(
    long ExperimentId,
    IReadOnlyList<long>? EmbeddingModelIds,
    IReadOnlyList<long>? ChunkingStrategyIds,
    IReadOnlyList<long>? LlmModelIds) : IRequest<int>;

public sealed class CreateRunsCommandHandler(IAppDbContext db) : IRequestHandler<CreateRunsCommand, int>
{
    public async Task<int> Handle(CreateRunsCommand request, CancellationToken ct)
    {
        var experiment = await db.Experiments.FirstOrDefaultAsync(e => e.Id == request.ExperimentId, ct)
            ?? throw new NotFoundException("Không tìm thấy thí nghiệm.");

        var cfg = await db.SystemConfigurations.AsNoTracking().FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("System configuration is missing.");

        var embeddingIds = Coalesce(request.EmbeddingModelIds, cfg.ActiveEmbeddingModelId);
        var strategyIds = Coalesce(request.ChunkingStrategyIds, cfg.ActiveChunkingStrategyId);
        var llmIds = Coalesce(request.LlmModelIds, cfg.ActiveLlmModelId);

        ValidateForType(experiment.Type, request);

        var embeddingNames = await db.EmbeddingModels.Where(m => embeddingIds.Contains(m.Id))
            .ToDictionaryAsync(m => m.Id, m => m.Name, ct);
        var strategyNames = await db.ChunkingStrategies.Where(s => strategyIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.Name, ct);
        var llmNames = await db.LlmModels.Where(m => llmIds.Contains(m.Id))
            .ToDictionaryAsync(m => m.Id, m => m.Name, ct);

        var created = 0;
        foreach (var e in embeddingIds)
        {
            foreach (var s in strategyIds)
            {
                foreach (var l in llmIds)
                {
                    var snapshot = JsonSerializer.Serialize(new
                    {
                        embeddingModel = embeddingNames.GetValueOrDefault(e),
                        chunkingStrategy = strategyNames.GetValueOrDefault(s),
                        llmModel = llmNames.GetValueOrDefault(l),
                    });
                    db.ExperimentRuns.Add(new ExperimentRun
                    {
                        ExperimentId = experiment.Id,
                        ExperimentType = experiment.Type,
                        EmbeddingModelId = e,
                        ChunkingStrategyId = s,
                        LlmModelId = l,
                        RunName = $"{embeddingNames.GetValueOrDefault(e)} | {strategyNames.GetValueOrDefault(s)} | {llmNames.GetValueOrDefault(l)}",
                        ConfigSnapshot = snapshot,
                        Status = RunStatus.Queued,
                    });
                    created++;
                }
            }
        }

        await db.SaveChangesAsync(ct);
        return created;
    }

    private static List<long> Coalesce(IReadOnlyList<long>? ids, long? fallback) =>
        ids is { Count: > 0 } ? [.. ids] : fallback is { } f ? [f] : [];

    private static void ValidateForType(ExperimentType type, CreateRunsCommand request)
    {
        var ok = type switch
        {
            ExperimentType.EmbeddingBench => request.EmbeddingModelIds is { Count: > 0 },
            ExperimentType.ChunkingBench => request.ChunkingStrategyIds is { Count: > 0 },
            ExperimentType.RagVsFinetune => request.LlmModelIds is { Count: > 0 },
            _ => true,
        };
        if (!ok)
        {
            throw new BusinessRuleException(
                "Loại thí nghiệm yêu cầu cung cấp danh sách model/strategy tương ứng.");
        }
    }
}

// ---- Start (enqueue queued runs) -----------------------------------------------
public sealed record StartExperimentCommand(long ExperimentId) : IRequest<int>;

public sealed class StartExperimentCommandHandler(IAppDbContext db, IJobScheduler scheduler)
    : IRequestHandler<StartExperimentCommand, int>
{
    public async Task<int> Handle(StartExperimentCommand request, CancellationToken ct)
    {
        var experiment = await db.Experiments.FirstOrDefaultAsync(e => e.Id == request.ExperimentId, ct)
            ?? throw new NotFoundException("Không tìm thấy thí nghiệm.");

        var queued = await db.ExperimentRuns
            .Where(r => r.ExperimentId == experiment.Id && r.Status == RunStatus.Queued)
            .ToListAsync(ct);
        if (queued.Count == 0)
        {
            throw new BusinessRuleException("Không có lần chạy nào ở trạng thái chờ. Hãy tạo runs trước.");
        }

        experiment.Status = ExperimentStatus.Running;
        await db.SaveChangesAsync(ct);

        foreach (var run in queued)
        {
            run.HangfireJobId = scheduler.EnqueueExperimentRun(run.Id);
        }

        await db.SaveChangesAsync(ct);
        return queued.Count;
    }
}

// ---- Results + dashboard -------------------------------------------------------
public sealed record GetRunResultsQuery(long RunId) : IRequest<IReadOnlyList<EvaluationResultDto>>;

public sealed class GetRunResultsQueryHandler(IAppDbContext db)
    : IRequestHandler<GetRunResultsQuery, IReadOnlyList<EvaluationResultDto>>
{
    public async Task<IReadOnlyList<EvaluationResultDto>> Handle(GetRunResultsQuery request, CancellationToken ct) =>
        await db.EvaluationResults.AsNoTracking()
            .Where(r => r.ExperimentRunId == request.RunId)
            .OrderBy(r => r.TestQuestionId)
            .Select(r => new EvaluationResultDto(
                r.TestQuestionId, r.TestQuestion.Question, r.GeneratedAnswer, r.PerQuestionStatus,
                r.Faithfulness, r.AnswerRelevancy, r.ContextPrecision, r.ContextRecall, r.AnswerCorrectness, r.LatencyMs))
            .ToListAsync(ct);
}

public sealed record GetExperimentDashboardQuery(long ExperimentId) : IRequest<IReadOnlyList<RunMetricDto>>;

public sealed class GetExperimentDashboardQueryHandler(IAppDbContext db)
    : IRequestHandler<GetExperimentDashboardQuery, IReadOnlyList<RunMetricDto>>
{
    public async Task<IReadOnlyList<RunMetricDto>> Handle(GetExperimentDashboardQuery request, CancellationToken ct) =>
        await db.ExperimentRuns.AsNoTracking()
            .Where(r => r.ExperimentId == request.ExperimentId)
            .OrderBy(r => r.Id)
            .Select(r => new RunMetricDto(
                r.Id, r.RunName, r.Status,
                r.EmbeddingModel!.Name, r.ChunkingStrategy!.Name, r.LlmModel!.Name,
                r.Metric!.AvgFaithfulness, r.Metric.AvgAnswerRelevancy, r.Metric.AvgContextPrecision,
                r.Metric.AvgContextRecall, r.Metric.AvgAnswerCorrectness, r.Metric.AvgLatencyMs,
                r.Metric != null ? r.Metric.TotalQuestions : 0))
            .ToListAsync(ct);
}
