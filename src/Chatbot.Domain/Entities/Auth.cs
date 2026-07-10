using Chatbot.Domain.Common;

namespace Chatbot.Domain.Entities;

/// <summary>An application user. Accounts are provisioned by an admin (no self-registration).</summary>
public class User : AuditableEntity, ISoftDeletable
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string NormalizedEmail { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>Bumped on password change, deactivation, or any role/permission change to revoke live tokens.</summary>
    public string SecurityStamp { get; set; } = Guid.NewGuid().ToString("N");

    public bool IsActive { get; set; } = true;
    public bool EmailConfirmed { get; set; }
    public DateTime? LastLoginUtc { get; set; }
    public int AccessFailedCount { get; set; }
    public DateTime? LockoutEndUtc { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public long? DeletedBy { get; set; }

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public ICollection<UserSubject> UserSubjects { get; set; } = new List<UserSubject>();
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}

/// <summary>A named RBAC role (Admin / Lecturer / Student). System roles cannot be deleted.</summary>
public class Role : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsSystem { get; set; }

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}

/// <summary>A fine-grained permission, e.g. "documents.upload". Seeded; codes are canonical.</summary>
public class Permission : Entity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string? Description { get; set; }
    public bool IsSystem { get; set; }

    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}

/// <summary>Join: a user has a role.</summary>
public class UserRole
{
    public long UserId { get; set; }
    public long RoleId { get; set; }
    public DateTime AssignedAtUtc { get; set; }
    public long? AssignedBy { get; set; }

    public User User { get; set; } = null!;
    public Role Role { get; set; } = null!;
}

/// <summary>Join: a role grants a permission.</summary>
public class RolePermission
{
    public long RoleId { get; set; }
    public long PermissionId { get; set; }

    public Role Role { get; set; } = null!;
    public Permission Permission { get; set; } = null!;
}

/// <summary>Join: a student is enrolled in a subject (scopes document/chat access).</summary>
public class UserSubject
{
    public long UserId { get; set; }
    public long SubjectId { get; set; }
    public DateTime EnrolledAtUtc { get; set; }

    public User User { get; set; } = null!;
    public Subject Subject { get; set; } = null!;
}

/// <summary>Join: an instructor is assigned to a subject (scopes who may upload/manage its documents).</summary>
public class SubjectInstructor
{
    public long SubjectId { get; set; }
    public long UserId { get; set; }
    public DateTime AssignedAtUtc { get; set; }
    public long? AssignedBy { get; set; }

    public Subject Subject { get; set; } = null!;
    public User User { get; set; } = null!;
}

/// <summary>A refresh token (stored as SHA-256 hash). Rotation revokes the whole family on reuse.</summary>
public class RefreshToken : CreatedEntity
{
    public long UserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public Guid FamilyId { get; set; }
    public string JwtId { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
    public string? CreatedByIp { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public string? RevokedByIp { get; set; }
    public string? ReplacedByTokenHash { get; set; }
    public string? ReasonRevoked { get; set; }

    public User User { get; set; } = null!;

    public bool IsActive => RevokedAtUtc is null && DateTime.UtcNow < ExpiresAtUtc;
}

/// <summary>A single-use password reset token (stored as SHA-256 hash).</summary>
public class PasswordResetToken : CreatedEntity
{
    public long UserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? UsedAtUtc { get; set; }

    public User User { get; set; } = null!;
}
