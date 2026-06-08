using Chatbot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chatbot.Infrastructure.Persistence.Configurations;

public sealed class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> b)
    {
        b.ToTable("Document", Schemas.Dbo);
        b.HasKey(x => x.Id);

        b.Property(x => x.Title).HasMaxLength(300).IsRequired().UseCollation(ColumnTypes.VietnameseText);
        b.Property(x => x.OriginalFileName).HasMaxLength(260).IsRequired();
        b.Property(x => x.StoredFileName).HasMaxLength(260).IsRequired();
        b.Property(x => x.ContentType).HasMaxLength(120).IsRequired();
        b.Property(x => x.FileExtension).HasMaxLength(10).IsRequired();
        b.Property(x => x.RelativePath).HasMaxLength(500).IsRequired();
        b.Property(x => x.StorageProvider).HasMaxLength(40).HasDefaultValue("LocalDisk");
        b.Property(x => x.Sha256Checksum).HasColumnType(ColumnTypes.Sha256)
            .UseCollation(ColumnTypes.BinaryCollation).IsRequired();
        b.Property(x => x.CreatedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.RowVersion).IsRowVersion();

        b.HasOne(x => x.Subject).WithMany(s => s.Documents)
            .HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Chapter).WithMany(c => c.Documents)
            .HasForeignKey(x => x.ChapterId).OnDelete(DeleteBehavior.SetNull);

        b.HasIndex(x => x.SubjectId).HasDatabaseName("IX_Document_SubjectId");
        b.HasIndex(x => x.ChapterId).HasDatabaseName("IX_Document_ChapterId");
        b.HasIndex(x => x.Status).HasDatabaseName("IX_Document_Status").HasFilter("[IsDeleted] = 0");
        b.HasIndex(x => new { x.SubjectId, x.Sha256Checksum })
            .IsUnique().HasDatabaseName("UQ_Document_Subject_Sha").HasFilter("[IsDeleted] = 0");

        b.HasQueryFilter(x => !x.IsDeleted);
    }
}

public sealed class DocumentProcessingJobConfiguration : IEntityTypeConfiguration<DocumentProcessingJob>
{
    public void Configure(EntityTypeBuilder<DocumentProcessingJob> b)
    {
        b.ToTable("DocumentProcessingJob", Schemas.Dbo);
        b.HasKey(x => x.Id);

        b.Property(x => x.HangfireJobId).HasMaxLength(64);
        b.Property(x => x.Detail).HasMaxLength(2000);
        b.Property(x => x.CreatedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");

        b.HasOne(x => x.Document).WithMany(d => d.ProcessingJobs)
            .HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<ChunkingStrategy>().WithMany()
            .HasForeignKey(x => x.ChunkingStrategyId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<EmbeddingModel>().WithMany()
            .HasForeignKey(x => x.EmbeddingModelId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => x.DocumentId).HasDatabaseName("IX_DocProcJob_DocumentId");
        b.HasIndex(x => x.DocumentId)
            .IsUnique()
            .HasDatabaseName("UQ_DocProcJob_ActivePerDoc")
            .HasFilter("[State] IN ('queued','running')");
    }
}
