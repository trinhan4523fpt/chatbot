using Chatbot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chatbot.Infrastructure.Persistence.Configurations;

public sealed class ChatSessionConfiguration : IEntityTypeConfiguration<ChatSession>
{
    public void Configure(EntityTypeBuilder<ChatSession> b)
    {
        b.ToTable("ChatSession", Schemas.Dbo);
        b.HasKey(x => x.Id);
        b.Property(x => x.Title).HasMaxLength(255);
        b.Property(x => x.CreatedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.RowVersion).IsRowVersion();

        b.HasOne(x => x.User).WithMany()
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Subject).WithMany()
            .HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Restrict);

        // Optional pins reference never-deleted registry rows.
        b.HasOne<EmbeddingModel>().WithMany()
            .HasForeignKey(x => x.PinnedEmbeddingModelId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ChunkingStrategy>().WithMany()
            .HasForeignKey(x => x.PinnedChunkingStrategyId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<LlmModel>().WithMany()
            .HasForeignKey(x => x.PinnedLlmModelId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => x.UserId).HasDatabaseName("IX_ChatSession_UserId");
        b.HasIndex(x => x.SubjectId).HasDatabaseName("IX_ChatSession_SubjectId");
        b.HasQueryFilter(x => !x.IsDeleted);
    }
}

public sealed class ChatMessageConfiguration : IEntityTypeConfiguration<ChatMessage>
{
    public void Configure(EntityTypeBuilder<ChatMessage> b)
    {
        b.ToTable("ChatMessage", Schemas.Dbo);
        b.HasKey(x => x.Id);
        b.Property(x => x.CreatedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");

        b.HasOne(x => x.Session).WithMany(s => s.Messages)
            .HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<LlmModel>().WithMany()
            .HasForeignKey(x => x.LlmModelId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<EmbeddingModel>().WithMany()
            .HasForeignKey(x => x.EmbeddingModelId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.SessionId, x.CreatedAtUtc })
            .HasDatabaseName("IX_ChatMessage_SessionId_CreatedAtUtc");
    }
}

public sealed class MessageCitationConfiguration : IEntityTypeConfiguration<MessageCitation>
{
    public void Configure(EntityTypeBuilder<MessageCitation> b)
    {
        b.ToTable("MessageCitation", Schemas.Dbo);
        b.HasKey(x => x.Id);

        // ChunkId/DocumentId are historical snapshots (no FK): chunks are physically replaced on
        // re-index and must not block deletion; DocumentTitle is denormalized for display.
        b.Property(x => x.DocumentTitle).HasMaxLength(300);
        b.Property(x => x.Snippet).HasMaxLength(2000);
        b.Property(x => x.RelevanceScore).HasPrecision(7, 6);
        b.Property(x => x.CreatedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");

        b.HasOne(x => x.Message).WithMany(m => m.Citations)
            .HasForeignKey(x => x.MessageId).OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => x.MessageId).HasDatabaseName("IX_MessageCitation_MessageId");
    }
}
