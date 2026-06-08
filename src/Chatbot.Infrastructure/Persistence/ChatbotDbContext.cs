using Chatbot.Application.Common.Interfaces;
using Chatbot.Domain.Entities;
using Chatbot.Domain.Enums;
using Chatbot.Infrastructure.Persistence.Conventions;
using Microsoft.EntityFrameworkCore;

namespace Chatbot.Infrastructure.Persistence;

public class ChatbotDbContext(DbContextOptions<ChatbotDbContext> options)
    : DbContext(options), IAppDbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserSubject> UserSubjects => Set<UserSubject>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();

    public DbSet<Subject> Subjects => Set<Subject>();
    public DbSet<Chapter> Chapters => Set<Chapter>();

    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentProcessingJob> DocumentProcessingJobs => Set<DocumentProcessingJob>();

    public DbSet<ChunkingStrategy> ChunkingStrategies => Set<ChunkingStrategy>();
    public DbSet<EmbeddingModel> EmbeddingModels => Set<EmbeddingModel>();
    public DbSet<LlmModel> LlmModels => Set<LlmModel>();
    public DbSet<SystemConfiguration> SystemConfigurations => Set<SystemConfiguration>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();

    public DbSet<DocumentChunk> DocumentChunks => Set<DocumentChunk>();
    public DbSet<ChunkEmbedding> ChunkEmbeddings => Set<ChunkEmbedding>();

    public DbSet<ChatSession> ChatSessions => Set<ChatSession>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<MessageCitation> MessageCitations => Set<MessageCitation>();

    public DbSet<Experiment> Experiments => Set<Experiment>();
    public DbSet<TestQuestion> TestQuestions => Set<TestQuestion>();
    public DbSet<ExperimentRun> ExperimentRuns => Set<ExperimentRun>();
    public DbSet<EvaluationResult> EvaluationResults => Set<EvaluationResult>();
    public DbSet<EvaluationRetrieval> EvaluationRetrievals => Set<EvaluationRetrieval>();
    public DbSet<ExperimentRunMetric> ExperimentRunMetrics => Set<ExperimentRunMetric>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<IntegrationOutbox> IntegrationOutbox => Set<IntegrationOutbox>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ChatbotDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        // UTC datetime2(7) for all DateTime columns.
        configurationBuilder.Properties<DateTime>()
            .HaveConversion<UtcDateTimeConverter>()
            .HaveColumnType("datetime2(7)");

        // Default decimal precision for RAGAS/relevance scores (0..1). Latency averages override to (10,2).
        configurationBuilder.Properties<decimal>().HavePrecision(7, 6);

        // Enums persisted as snake_case tokens.
        configurationBuilder.Properties<FileType>().HaveConversion<SnakeCaseEnumConverter<FileType>>().HaveMaxLength(20);
        configurationBuilder.Properties<DocumentStatus>().HaveConversion<SnakeCaseEnumConverter<DocumentStatus>>().HaveMaxLength(20);
        configurationBuilder.Properties<ProcessingStage>().HaveConversion<SnakeCaseEnumConverter<ProcessingStage>>().HaveMaxLength(20);
        configurationBuilder.Properties<ProcessingState>().HaveConversion<SnakeCaseEnumConverter<ProcessingState>>().HaveMaxLength(20);
        configurationBuilder.Properties<ChunkEmbeddingStatus>().HaveConversion<SnakeCaseEnumConverter<ChunkEmbeddingStatus>>().HaveMaxLength(20);
        configurationBuilder.Properties<ChatRole>().HaveConversion<SnakeCaseEnumConverter<ChatRole>>().HaveMaxLength(20);
        configurationBuilder.Properties<ChatMessageStatus>().HaveConversion<SnakeCaseEnumConverter<ChatMessageStatus>>().HaveMaxLength(20);
        configurationBuilder.Properties<LlmModelType>().HaveConversion<SnakeCaseEnumConverter<LlmModelType>>().HaveMaxLength(20);
        configurationBuilder.Properties<ExperimentType>().HaveConversion<SnakeCaseEnumConverter<ExperimentType>>().HaveMaxLength(30);
        configurationBuilder.Properties<ExperimentStatus>().HaveConversion<SnakeCaseEnumConverter<ExperimentStatus>>().HaveMaxLength(20);
        configurationBuilder.Properties<RunStatus>().HaveConversion<SnakeCaseEnumConverter<RunStatus>>().HaveMaxLength(20);
        configurationBuilder.Properties<PerQuestionStatus>().HaveConversion<SnakeCaseEnumConverter<PerQuestionStatus>>().HaveMaxLength(20);
        configurationBuilder.Properties<Difficulty>().HaveConversion<SnakeCaseEnumConverter<Difficulty>>().HaveMaxLength(10);
        configurationBuilder.Properties<OutboxStatus>().HaveConversion<SnakeCaseEnumConverter<OutboxStatus>>().HaveMaxLength(20);
        configurationBuilder.Properties<OutboxEventType>().HaveConversion<SnakeCaseEnumConverter<OutboxEventType>>().HaveMaxLength(30);
    }
}
