using Chatbot.Domain.Common;
using Chatbot.Domain.Enums;

namespace Chatbot.Domain.Entities;

/// <summary>A research experiment comparing RAG vs fine-tune, chunking, or embedding models.</summary>
public class Experiment : AuditableEntity, ISoftDeletable
{
    public string Name { get; set; } = string.Empty;
    public ExperimentType Type { get; set; }
    public string? Description { get; set; }
    public ExperimentStatus Status { get; set; } = ExperimentStatus.Draft;
    public long? SubjectId { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public long? DeletedBy { get; set; }

    public Subject? Subject { get; set; }
    public ICollection<ExperimentRun> Runs { get; set; } = new List<ExperimentRun>();
}

/// <summary>A test question + human ground truth (the 50-question test set).</summary>
public class TestQuestion : AuditableEntity, ISoftDeletable
{
    public long SubjectId { get; set; }
    public string Question { get; set; } = string.Empty;
    public string GroundTruth { get; set; } = string.Empty;
    public string? ReferenceContext { get; set; }
    public Difficulty? Difficulty { get; set; }
    public string? ExternalRef { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public long? DeletedBy { get; set; }

    public Subject Subject { get; set; } = null!;
}

/// <summary>One configured run of an experiment (a point in the embedding x chunking x llm space).</summary>
public class ExperimentRun : AuditableEntity
{
    public long ExperimentId { get; set; }
    public ExperimentType ExperimentType { get; set; }
    public long? EmbeddingModelId { get; set; }
    public long? ChunkingStrategyId { get; set; }
    public long? LlmModelId { get; set; }

    public string RunName { get; set; } = string.Empty;
    public string? Params { get; set; }
    public string? ConfigSnapshot { get; set; }
    public string? CorpusSnapshot { get; set; }

    public RunStatus Status { get; set; } = RunStatus.Queued;
    public string? HangfireJobId { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? FinishedAtUtc { get; set; }

    public Experiment Experiment { get; set; } = null!;
    public EmbeddingModel? EmbeddingModel { get; set; }
    public ChunkingStrategy? ChunkingStrategy { get; set; }
    public LlmModel? LlmModel { get; set; }
    public ICollection<EvaluationResult> Results { get; set; } = new List<EvaluationResult>();
    public ExperimentRunMetric? Metric { get; set; }
}

/// <summary>Per-question evaluation result for a run, with the 5 RAGAS metrics.</summary>
public class EvaluationResult : CreatedEntity
{
    public long ExperimentRunId { get; set; }
    public long TestQuestionId { get; set; }
    public string? GeneratedAnswer { get; set; }
    public string? RetrievedContexts { get; set; }

    public decimal? Faithfulness { get; set; }
    public decimal? AnswerRelevancy { get; set; }
    public decimal? ContextPrecision { get; set; }
    public decimal? ContextRecall { get; set; }
    public decimal? AnswerCorrectness { get; set; }
    public int? LatencyMs { get; set; }

    public PerQuestionStatus PerQuestionStatus { get; set; } = PerQuestionStatus.Pending;

    public ExperimentRun ExperimentRun { get; set; } = null!;
    public TestQuestion TestQuestion { get; set; } = null!;
    public ICollection<EvaluationRetrieval> Retrievals { get; set; } = new List<EvaluationRetrieval>();
}

/// <summary>One retrieved context for an evaluation result (drill-down for context precision/recall).</summary>
public class EvaluationRetrieval : Entity
{
    public long EvaluationResultId { get; set; }
    public long? ChunkId { get; set; }
    public long? DocumentId { get; set; }
    public int Rank { get; set; }
    public decimal? Score { get; set; }
    public string? Snippet { get; set; }

    public EvaluationResult EvaluationResult { get; set; } = null!;
}

/// <summary>Aggregated metrics for a run (one row per run), used by the dashboard.</summary>
public class ExperimentRunMetric : Entity
{
    public long ExperimentRunId { get; set; }
    public decimal? AvgFaithfulness { get; set; }
    public decimal? AvgAnswerRelevancy { get; set; }
    public decimal? AvgContextPrecision { get; set; }
    public decimal? AvgContextRecall { get; set; }
    public decimal? AvgAnswerCorrectness { get; set; }
    public decimal? AvgLatencyMs { get; set; }
    // Additional retrieval / chunking metrics
    public int? ChunkCount { get; set; }
    public decimal? AvgTokens { get; set; }
    public decimal? ChunkingTimeMs { get; set; }
    public decimal? RecallAtK { get; set; }
    public decimal? PrecisionAtK { get; set; }
    public decimal? Mrr { get; set; }
    public decimal? Ndcg { get; set; }
    public int TotalQuestions { get; set; }
    public DateTime ComputedAtUtc { get; set; }

    public ExperimentRun ExperimentRun { get; set; } = null!;
}
