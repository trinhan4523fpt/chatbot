namespace Chatbot.Domain.Common;

/// <summary>Marks an entity that records when it was created.</summary>
public interface IHasCreatedAt
{
    DateTime CreatedAtUtc { get; set; }
}

/// <summary>Full create/update audit trail. Set by the SaveChanges interceptor.</summary>
public interface IAuditableEntity : IHasCreatedAt
{
    DateTime? UpdatedAtUtc { get; set; }
    long? CreatedBy { get; set; }
    long? UpdatedBy { get; set; }
}

/// <summary>Soft-deletable entity. A global query filter hides rows where IsDeleted = true.</summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; set; }
    DateTime? DeletedAtUtc { get; set; }
    long? DeletedBy { get; set; }
}

/// <summary>Carries a SQL Server rowversion for optimistic concurrency.</summary>
public interface IConcurrencyAware
{
    byte[] RowVersion { get; set; }
}

/// <summary>Base for all entities with a surrogate BIGINT identity key.</summary>
public abstract class Entity
{
    public long Id { get; set; }
}

/// <summary>Append-only / derived entity that only records its creation time.</summary>
public abstract class CreatedEntity : Entity, IHasCreatedAt
{
    public DateTime CreatedAtUtc { get; set; }
}

/// <summary>User-editable business entity: full audit trail + optimistic concurrency.</summary>
public abstract class AuditableEntity : CreatedEntity, IAuditableEntity, IConcurrencyAware
{
    public DateTime? UpdatedAtUtc { get; set; }
    public long? CreatedBy { get; set; }
    public long? UpdatedBy { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
