using Chatbot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chatbot.Infrastructure.Persistence.Configurations;

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        b.ToTable("AuditLog", Schemas.Dbo, t =>
        {
            t.HasCheckConstraint("CK_AuditLog_OldValues_Json", "[OldValues] IS NULL OR ISJSON([OldValues]) = 1");
            t.HasCheckConstraint("CK_AuditLog_NewValues_Json", "[NewValues] IS NULL OR ISJSON([NewValues]) = 1");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.ActorEmail).HasMaxLength(256);
        b.Property(x => x.Action).HasMaxLength(100).IsRequired();
        b.Property(x => x.TargetType).HasMaxLength(100);
        b.Property(x => x.TargetId).HasMaxLength(100);
        b.Property(x => x.IpAddress).HasMaxLength(64);
        b.Property(x => x.UserAgent).HasMaxLength(512);
        b.Property(x => x.CreatedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");

        // No FK on Actor/Target user ids: the audit trail must survive user hard-deletes (if any).
        b.HasIndex(x => x.ActorUserId).HasDatabaseName("IX_AuditLog_ActorUserId");
        b.HasIndex(x => x.TargetUserId).HasDatabaseName("IX_AuditLog_TargetUserId");
        b.HasIndex(x => new { x.TargetType, x.TargetId }).HasDatabaseName("IX_AuditLog_Target");
        b.HasIndex(x => x.CreatedAtUtc).HasDatabaseName("IX_AuditLog_CreatedAtUtc");
    }
}

public sealed class IntegrationOutboxConfiguration : IEntityTypeConfiguration<IntegrationOutbox>
{
    public void Configure(EntityTypeBuilder<IntegrationOutbox> b)
    {
        b.ToTable("IntegrationOutbox", Schemas.Dbo, t =>
            t.HasCheckConstraint("CK_Outbox_Payload_Json", "[Payload] IS NULL OR ISJSON([Payload]) = 1"));
        b.HasKey(x => x.Id);
        b.Property(x => x.Payload).IsRequired();
        b.Property(x => x.Error).HasMaxLength(2000);
        b.Property(x => x.CreatedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.AvailableAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");

        b.HasIndex(x => x.Status).HasDatabaseName("IX_Outbox_Pending").HasFilter("[Status] = 'pending'");
    }
}
