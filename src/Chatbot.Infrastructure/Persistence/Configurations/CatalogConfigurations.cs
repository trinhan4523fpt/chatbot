using Chatbot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chatbot.Infrastructure.Persistence.Configurations;

public sealed class SubjectConfiguration : IEntityTypeConfiguration<Subject>
{
    public void Configure(EntityTypeBuilder<Subject> b)
    {
        b.ToTable("Subject", Schemas.Dbo);
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(50).IsRequired();
        b.Property(x => x.Name).HasMaxLength(255).IsRequired().UseCollation(ColumnTypes.VietnameseText);
        b.Property(x => x.CreatedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.RowVersion).IsRowVersion();

        b.HasIndex(x => x.Code).IsUnique()
            .HasDatabaseName("UQ_Subject_Code").HasFilter("[IsDeleted] = 0");

        b.HasQueryFilter(x => !x.IsDeleted);
    }
}

public sealed class ChapterConfiguration : IEntityTypeConfiguration<Chapter>
{
    public void Configure(EntityTypeBuilder<Chapter> b)
    {
        b.ToTable("Chapter", Schemas.Dbo);
        b.HasKey(x => x.Id);
        b.Property(x => x.Title).HasMaxLength(255).IsRequired().UseCollation(ColumnTypes.VietnameseText);
        b.Property(x => x.OrderIndex).HasDefaultValue(0);
        b.Property(x => x.CreatedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.RowVersion).IsRowVersion();

        b.HasOne(x => x.Subject).WithMany(s => s.Chapters)
            .HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => x.SubjectId).HasDatabaseName("IX_Chapter_SubjectId");
        b.HasQueryFilter(x => !x.IsDeleted);
    }
}
