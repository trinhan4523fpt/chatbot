using Chatbot.Domain.Enums;

namespace Chatbot.Application.Features.Experiments;

public sealed record TestQuestionDto(
    long Id, long SubjectId, string Question, string GroundTruth, string? ReferenceContext,
    Difficulty? Difficulty, string? ExternalRef);

public sealed record ExperimentDto(
    long Id, string Name, ExperimentType Type, ExperimentStatus Status, long? SubjectId,
    int RunCount, DateTime CreatedAtUtc);

public sealed record ExperimentRunDto(
    long Id, long ExperimentId, string RunName, RunStatus Status,
    long? EmbeddingModelId, long? ChunkingStrategyId, long? LlmModelId,
    DateTime? StartedAtUtc, DateTime? FinishedAtUtc);

public sealed record EvaluationResultDto(
    long TestQuestionId, string Question, string? GeneratedAnswer, PerQuestionStatus Status,
    decimal? Faithfulness, decimal? AnswerRelevancy, decimal? ContextPrecision,
    decimal? ContextRecall, decimal? AnswerCorrectness, int? LatencyMs);

public sealed record RunMetricDto(
    long ExperimentRunId, string RunName, RunStatus Status,
    string? EmbeddingModel, string? ChunkingStrategy, string? LlmModel,
    decimal? AvgFaithfulness, decimal? AvgAnswerRelevancy, decimal? AvgContextPrecision,
    decimal? AvgContextRecall, decimal? AvgAnswerCorrectness, decimal? AvgLatencyMs, int TotalQuestions);
