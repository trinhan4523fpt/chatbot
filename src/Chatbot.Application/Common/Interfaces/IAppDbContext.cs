using Chatbot.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Chatbot.Application.Common.Interfaces;

/// <summary>Application-facing view of the database. Implemented by the EF Core DbContext.</summary>
public interface IAppDbContext
{
    // Auth / RBAC
    DbSet<User> Users { get; }
    DbSet<Role> Roles { get; }
    DbSet<Permission> Permissions { get; }
    DbSet<UserRole> UserRoles { get; }
    DbSet<RolePermission> RolePermissions { get; }
    DbSet<UserSubject> UserSubjects { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<PasswordResetToken> PasswordResetTokens { get; }

    // Catalog
    DbSet<Subject> Subjects { get; }
    DbSet<Chapter> Chapters { get; }

    // Documents
    DbSet<Document> Documents { get; }
    DbSet<DocumentProcessingJob> DocumentProcessingJobs { get; }

    // Config / reference
    DbSet<ChunkingStrategy> ChunkingStrategies { get; }
    DbSet<EmbeddingModel> EmbeddingModels { get; }
    DbSet<LlmModel> LlmModels { get; }
    DbSet<SystemConfiguration> SystemConfigurations { get; }
    DbSet<AppSetting> AppSettings { get; }

    // RAG index linkage
    DbSet<DocumentChunk> DocumentChunks { get; }
    DbSet<ChunkEmbedding> ChunkEmbeddings { get; }

    // Chat
    DbSet<ChatSession> ChatSessions { get; }
    DbSet<ChatMessage> ChatMessages { get; }
    DbSet<MessageCitation> MessageCitations { get; }

    // Research (RBL)
    DbSet<Experiment> Experiments { get; }
    DbSet<TestQuestion> TestQuestions { get; }
    DbSet<ExperimentRun> ExperimentRuns { get; }
    DbSet<EvaluationResult> EvaluationResults { get; }
    DbSet<EvaluationRetrieval> EvaluationRetrievals { get; }
    DbSet<ExperimentRunMetric> ExperimentRunMetrics { get; }

    // Cross-cutting
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<IntegrationOutbox> IntegrationOutbox { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
