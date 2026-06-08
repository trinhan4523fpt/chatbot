using Chatbot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chatbot.Infrastructure.Persistence.Configurations;

public sealed class ChunkingStrategyConfiguration : IEntityTypeConfiguration<ChunkingStrategy>
{
    public void Configure(EntityTypeBuilder<ChunkingStrategy> b)
    {
        b.ToTable("ChunkingStrategy", Schemas.Dbo, t =>
            t.HasCheckConstraint("CK_ChunkingStrategy_Params_Json", "[Params] IS NULL OR ISJSON([Params]) = 1"));
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.Description).HasMaxLength(2000);
        b.Property(x => x.CreatedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => x.Name).IsUnique().HasDatabaseName("UQ_ChunkingStrategy_Name");
    }
}

public sealed class EmbeddingModelConfiguration : IEntityTypeConfiguration<EmbeddingModel>
{
    public void Configure(EntityTypeBuilder<EmbeddingModel> b)
    {
        b.ToTable("EmbeddingModel", Schemas.Dbo);
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(150).IsRequired();
        b.Property(x => x.Provider).HasMaxLength(100);
        b.Property(x => x.QdrantCollectionName).HasMaxLength(100).IsRequired();
        b.Property(x => x.Description).HasMaxLength(2000);
        b.Property(x => x.CreatedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => x.Name).IsUnique().HasDatabaseName("UQ_EmbeddingModel_Name");
        b.HasIndex(x => x.QdrantCollectionName).IsUnique().HasDatabaseName("UQ_EmbeddingModel_Collection");
    }
}

public sealed class LlmModelConfiguration : IEntityTypeConfiguration<LlmModel>
{
    public void Configure(EntityTypeBuilder<LlmModel> b)
    {
        b.ToTable("LlmModel", Schemas.Dbo);
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(150).IsRequired();
        b.Property(x => x.Provider).HasMaxLength(100);
        b.Property(x => x.BaseModel).HasMaxLength(150);
        b.Property(x => x.Description).HasMaxLength(2000);
        b.Property(x => x.CreatedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => x.Name).IsUnique().HasDatabaseName("UQ_LlmModel_Name");
    }
}

public sealed class SystemConfigurationConfiguration : IEntityTypeConfiguration<SystemConfiguration>
{
    public void Configure(EntityTypeBuilder<SystemConfiguration> b)
    {
        b.ToTable("SystemConfiguration", Schemas.Dbo, t =>
            t.HasCheckConstraint("CK_SystemConfiguration_Singleton", "[Id] = 1"));
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.MinRelevanceScore).HasPrecision(7, 6);
        b.Property(x => x.CreatedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.RowVersion).IsRowVersion();

        b.HasOne(x => x.ActiveEmbeddingModel).WithMany()
            .HasForeignKey(x => x.ActiveEmbeddingModelId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.ActiveChunkingStrategy).WithMany()
            .HasForeignKey(x => x.ActiveChunkingStrategyId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.ActiveLlmModel).WithMany()
            .HasForeignKey(x => x.ActiveLlmModelId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class AppSettingConfiguration : IEntityTypeConfiguration<AppSetting>
{
    public void Configure(EntityTypeBuilder<AppSetting> b)
    {
        b.ToTable("AppSetting", Schemas.Dbo);
        b.HasKey(x => x.Id);
        b.Property(x => x.Key).HasMaxLength(150).IsRequired();
        b.Property(x => x.ValueType).HasMaxLength(50);
        b.Property(x => x.Description).HasMaxLength(500);
        b.HasIndex(x => x.Key).IsUnique().HasDatabaseName("UQ_AppSetting_Key");
    }
}
