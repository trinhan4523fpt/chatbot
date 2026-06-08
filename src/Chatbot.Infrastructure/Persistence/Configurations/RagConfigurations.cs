using Chatbot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chatbot.Infrastructure.Persistence.Configurations;

public sealed class DocumentChunkConfiguration : IEntityTypeConfiguration<DocumentChunk>
{
    public void Configure(EntityTypeBuilder<DocumentChunk> b)
    {
        b.ToTable("DocumentChunk", Schemas.Rag, t =>
            t.HasCheckConstraint("CK_DocumentChunk_Metadata_Json", "[Metadata] IS NULL OR ISJSON([Metadata]) = 1"));
        b.HasKey(x => x.Id);

        b.Property(x => x.ContentHash).HasColumnType(ColumnTypes.Sha256).UseCollation(ColumnTypes.BinaryCollation);
        b.Property(x => x.CreatedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");

        b.HasOne(x => x.Document).WithMany(d => d.Chunks)
            .HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.ChunkingStrategy).WithMany()
            .HasForeignKey(x => x.ChunkingStrategyId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.DocumentId, x.ChunkingStrategyId, x.ChunkIndex })
            .IsUnique().HasDatabaseName("UQ_DocumentChunk_Doc_Strategy_Index");
        b.HasIndex(x => x.DocumentId)
            .HasDatabaseName("IX_Chunk_DocumentId")
            .IncludeProperties(x => new { x.ChunkingStrategyId, x.ChunkIndex });
    }
}

public sealed class ChunkEmbeddingConfiguration : IEntityTypeConfiguration<ChunkEmbedding>
{
    public void Configure(EntityTypeBuilder<ChunkEmbedding> b)
    {
        b.ToTable("ChunkEmbedding", Schemas.Rag);
        b.HasKey(x => x.Id);

        b.Property(x => x.VectorCollection).HasMaxLength(100).IsRequired();
        b.Property(x => x.CreatedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");

        b.HasOne(x => x.Chunk).WithMany(c => c.Embeddings)
            .HasForeignKey(x => x.ChunkId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.EmbeddingModel).WithMany()
            .HasForeignKey(x => x.EmbeddingModelId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.ChunkId, x.EmbeddingModelId })
            .IsUnique().HasDatabaseName("UQ_ChunkEmbedding_Chunk_Model");
        b.HasIndex(x => new { x.VectorCollection, x.VectorPointId })
            .IsUnique().HasDatabaseName("UQ_ChunkEmbedding_PointId");
        b.HasIndex(x => x.EmbeddingModelId)
            .HasDatabaseName("IX_ChunkEmbedding_Model")
            .IncludeProperties(x => new { x.ChunkId, x.VectorCollection, x.VectorPointId, x.Status });
    }
}
