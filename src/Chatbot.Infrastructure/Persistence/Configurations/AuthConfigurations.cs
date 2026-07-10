using Chatbot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chatbot.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("User", Schemas.Auth);
        b.HasKey(x => x.Id);

        b.Property(x => x.FullName).HasMaxLength(200).IsRequired();
        b.Property(x => x.Email).HasMaxLength(256).IsRequired().UseCollation(ColumnTypes.EmailCollation);
        b.Property(x => x.NormalizedEmail).HasMaxLength(256).IsRequired().UseCollation(ColumnTypes.EmailCollation);
        b.Property(x => x.PasswordHash).IsRequired();
        b.Property(x => x.SecurityStamp).HasMaxLength(64).IsRequired();
        b.Property(x => x.CreatedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.RowVersion).IsRowVersion();

        b.HasIndex(x => x.NormalizedEmail)
            .IsUnique()
            .HasDatabaseName("UQ_User_NormalizedEmail")
            .HasFilter("[IsDeleted] = 0");

        b.HasQueryFilter(x => !x.IsDeleted);
    }
}

public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> b)
    {
        b.ToTable("Role", Schemas.Auth);
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.NormalizedName).HasMaxLength(100).IsRequired();
        b.Property(x => x.Description).HasMaxLength(500);
        b.Property(x => x.CreatedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => x.NormalizedName).IsUnique().HasDatabaseName("UQ_Role_NormalizedName");
    }
}

public sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> b)
    {
        b.ToTable("Permission", Schemas.Auth);
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(100).IsRequired();
        b.Property(x => x.Name).HasMaxLength(150).IsRequired();
        b.Property(x => x.Category).HasMaxLength(100);
        b.Property(x => x.Description).HasMaxLength(500);
        b.HasIndex(x => x.Code).IsUnique().HasDatabaseName("UQ_Permission_Code");
    }
}

public sealed class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> b)
    {
        b.ToTable("UserRole", Schemas.Auth);
        b.HasKey(x => new { x.UserId, x.RoleId });
        b.Property(x => x.AssignedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");

        b.HasOne(x => x.User).WithMany(u => u.UserRoles)
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Role).WithMany(r => r.UserRoles)
            .HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => x.RoleId).HasDatabaseName("IX_UserRole_RoleId");
    }
}

public sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> b)
    {
        b.ToTable("RolePermission", Schemas.Auth);
        b.HasKey(x => new { x.RoleId, x.PermissionId });

        b.HasOne(x => x.Role).WithMany(r => r.RolePermissions)
            .HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Cascade);
        // NO ACTION: deleting a permission must not silently strip role grants.
        b.HasOne(x => x.Permission).WithMany(p => p.RolePermissions)
            .HasForeignKey(x => x.PermissionId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => x.PermissionId).HasDatabaseName("IX_RolePermission_PermissionId");
    }
}

public sealed class UserSubjectConfiguration : IEntityTypeConfiguration<UserSubject>
{
    public void Configure(EntityTypeBuilder<UserSubject> b)
    {
        b.ToTable("UserSubject", Schemas.Auth);
        b.HasKey(x => new { x.UserId, x.SubjectId });
        b.Property(x => x.EnrolledAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");

        b.HasOne(x => x.User).WithMany(u => u.UserSubjects)
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Subject).WithMany()
            .HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => x.SubjectId).HasDatabaseName("IX_UserSubject_SubjectId");
    }
}

public sealed class SubjectInstructorConfiguration : IEntityTypeConfiguration<SubjectInstructor>
{
    public void Configure(EntityTypeBuilder<SubjectInstructor> b)
    {
        b.ToTable("SubjectInstructor", Schemas.Auth);
        b.HasKey(x => new { x.SubjectId, x.UserId });
        b.Property(x => x.AssignedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");

        b.HasOne(x => x.User).WithMany()
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Subject).WithMany(s => s.Instructors)
            .HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Restrict);

        // One instructor per subject (backstop for the application-level check).
        b.HasIndex(x => x.SubjectId).IsUnique().HasDatabaseName("UQ_SubjectInstructor_SubjectId");
        b.HasIndex(x => x.UserId).HasDatabaseName("IX_SubjectInstructor_UserId");
    }
}

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> b)
    {
        b.ToTable("RefreshToken", Schemas.Auth);
        b.HasKey(x => x.Id);
        b.Ignore(x => x.IsActive);

        b.Property(x => x.TokenHash).HasColumnType(ColumnTypes.Sha256)
            .UseCollation(ColumnTypes.BinaryCollation).IsRequired();
        b.Property(x => x.JwtId).HasMaxLength(64);
        b.Property(x => x.CreatedByIp).HasMaxLength(64);
        b.Property(x => x.RevokedByIp).HasMaxLength(64);
        b.Property(x => x.ReplacedByTokenHash).HasColumnType(ColumnTypes.Sha256)
            .UseCollation(ColumnTypes.BinaryCollation);
        b.Property(x => x.ReasonRevoked).HasMaxLength(200);
        b.Property(x => x.CreatedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");

        b.HasOne(x => x.User).WithMany(u => u.RefreshTokens)
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => x.TokenHash).IsUnique().HasDatabaseName("UQ_RefreshToken_TokenHash");
        b.HasIndex(x => x.UserId).HasDatabaseName("IX_RefreshToken_UserId");
        b.HasIndex(x => x.FamilyId).HasDatabaseName("IX_RefreshToken_FamilyId");
    }
}

public sealed class PasswordResetTokenConfiguration : IEntityTypeConfiguration<PasswordResetToken>
{
    public void Configure(EntityTypeBuilder<PasswordResetToken> b)
    {
        b.ToTable("PasswordResetToken", Schemas.Auth);
        b.HasKey(x => x.Id);
        b.Property(x => x.TokenHash).HasColumnType(ColumnTypes.Sha256)
            .UseCollation(ColumnTypes.BinaryCollation).IsRequired();
        b.Property(x => x.CreatedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");

        b.HasOne(x => x.User).WithMany()
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => x.TokenHash).HasDatabaseName("IX_PasswordResetToken_TokenHash");
    }
}
