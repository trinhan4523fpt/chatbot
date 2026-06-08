using Chatbot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chatbot.Infrastructure.Persistence.Configurations;

public sealed class ExperimentConfiguration : IEntityTypeConfiguration<Experiment>
{
    public void Configure(EntityTypeBuilder<Experiment> b)
    {
        b.ToTable("Experiment", Schemas.Rbl);
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(255).IsRequired();
        b.Property(x => x.Description).HasMaxLength(2000);
        b.Property(x => x.CreatedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.RowVersion).IsRowVersion();

        b.HasOne(x => x.Subject).WithMany()
            .HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Restrict);

        b.HasQueryFilter(x => !x.IsDeleted);
    }
}

public sealed class TestQuestionConfiguration : IEntityTypeConfiguration<TestQuestion>
{
    public void Configure(EntityTypeBuilder<TestQuestion> b)
    {
        b.ToTable("TestQuestion", Schemas.Rbl);
        b.HasKey(x => x.Id);
        b.Property(x => x.ExternalRef).HasMaxLength(20);
        b.Property(x => x.CreatedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.RowVersion).IsRowVersion();

        b.HasOne(x => x.Subject).WithMany()
            .HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => x.SubjectId).HasDatabaseName("IX_TestQuestion_SubjectId");
        b.HasQueryFilter(x => !x.IsDeleted);
    }
}

public sealed class ExperimentRunConfiguration : IEntityTypeConfiguration<ExperimentRun>
{
    public void Configure(EntityTypeBuilder<ExperimentRun> b)
    {
        b.ToTable("ExperimentRun", Schemas.Rbl, t =>
        {
            t.HasCheckConstraint("CK_Run_Params_Json", "[Params] IS NULL OR ISJSON([Params]) = 1");
            t.HasCheckConstraint("CK_Run_ConfigSnapshot_Json", "[ConfigSnapshot] IS NULL OR ISJSON([ConfigSnapshot]) = 1");
            t.HasCheckConstraint("CK_Run_CorpusSnapshot_Json", "[CorpusSnapshot] IS NULL OR ISJSON([CorpusSnapshot]) = 1");
            t.HasCheckConstraint(
                "CK_Run_TypeModel",
                "([ExperimentType] = 'embedding_bench' AND [EmbeddingModelId] IS NOT NULL) OR " +
                "([ExperimentType] = 'chunking_bench' AND [ChunkingStrategyId] IS NOT NULL) OR " +
                "([ExperimentType] = 'rag_vs_finetune' AND [LlmModelId] IS NOT NULL)");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.RunName).HasMaxLength(200).IsRequired();
        b.Property(x => x.HangfireJobId).HasMaxLength(64);
        b.Property(x => x.CreatedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.RowVersion).IsRowVersion();

        b.HasOne(x => x.Experiment).WithMany(e => e.Runs)
            .HasForeignKey(x => x.ExperimentId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.EmbeddingModel).WithMany()
            .HasForeignKey(x => x.EmbeddingModelId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.ChunkingStrategy).WithMany()
            .HasForeignKey(x => x.ChunkingStrategyId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.LlmModel).WithMany()
            .HasForeignKey(x => x.LlmModelId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => x.ExperimentId).HasDatabaseName("IX_ExperimentRun_ExperimentId");
        b.HasIndex(x => x.Status).HasDatabaseName("IX_ExperimentRun_Status")
            .HasFilter("[Status] IN ('queued','running')");
    }
}

public sealed class EvaluationResultConfiguration : IEntityTypeConfiguration<EvaluationResult>
{
    public void Configure(EntityTypeBuilder<EvaluationResult> b)
    {
        b.ToTable("EvaluationResult", Schemas.Rbl, t =>
        {
            t.HasCheckConstraint("CK_Eval_Contexts_Json", "[RetrievedContexts] IS NULL OR ISJSON([RetrievedContexts]) = 1");
            t.HasCheckConstraint("CK_Eval_Faith", "[Faithfulness] IS NULL OR [Faithfulness] BETWEEN 0 AND 1");
            t.HasCheckConstraint("CK_Eval_AnsRel", "[AnswerRelevancy] IS NULL OR [AnswerRelevancy] BETWEEN 0 AND 1");
            t.HasCheckConstraint("CK_Eval_CtxPrec", "[ContextPrecision] IS NULL OR [ContextPrecision] BETWEEN 0 AND 1");
            t.HasCheckConstraint("CK_Eval_CtxRec", "[ContextRecall] IS NULL OR [ContextRecall] BETWEEN 0 AND 1");
            t.HasCheckConstraint("CK_Eval_AnsCorr", "[AnswerCorrectness] IS NULL OR [AnswerCorrectness] BETWEEN 0 AND 1");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.CreatedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");

        b.HasOne(x => x.ExperimentRun).WithMany(r => r.Results)
            .HasForeignKey(x => x.ExperimentRunId).OnDelete(DeleteBehavior.Cascade);
        // NO ACTION: avoids a second cascade path into EvaluationResult.
        b.HasOne(x => x.TestQuestion).WithMany()
            .HasForeignKey(x => x.TestQuestionId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.ExperimentRunId, x.TestQuestionId })
            .IsUnique().HasDatabaseName("UQ_EvaluationResult_Run_Question");
        b.HasIndex(x => x.ExperimentRunId).HasDatabaseName("IX_EvaluationResult_RunId");
    }
}

public sealed class EvaluationRetrievalConfiguration : IEntityTypeConfiguration<EvaluationRetrieval>
{
    public void Configure(EntityTypeBuilder<EvaluationRetrieval> b)
    {
        b.ToTable("EvaluationRetrieval", Schemas.Rbl);
        b.HasKey(x => x.Id);
        b.Property(x => x.Snippet).HasMaxLength(2000);

        // ChunkId/DocumentId are historical snapshots (no FK).
        b.HasOne(x => x.EvaluationResult).WithMany(r => r.Retrievals)
            .HasForeignKey(x => x.EvaluationResultId).OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => x.EvaluationResultId).HasDatabaseName("IX_EvaluationRetrieval_ResultId");
    }
}

public sealed class ExperimentRunMetricConfiguration : IEntityTypeConfiguration<ExperimentRunMetric>
{
    public void Configure(EntityTypeBuilder<ExperimentRunMetric> b)
    {
        b.ToTable("ExperimentRunMetric", Schemas.Rbl);
        b.HasKey(x => x.Id);
        b.Property(x => x.AvgLatencyMs).HasPrecision(10, 2);

        b.HasOne(x => x.ExperimentRun).WithOne(r => r.Metric)
            .HasForeignKey<ExperimentRunMetric>(x => x.ExperimentRunId).OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => x.ExperimentRunId).IsUnique().HasDatabaseName("UQ_ExperimentRunMetric_RunId");
    }
}
