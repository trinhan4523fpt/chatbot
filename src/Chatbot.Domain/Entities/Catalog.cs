using Chatbot.Domain.Common;

namespace Chatbot.Domain.Entities;

/// <summary>A course/subject. The demo seeds one subject.</summary>
public class Subject : AuditableEntity, ISoftDeletable
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public long? DeletedBy { get; set; }

    public ICollection<Chapter> Chapters { get; set; } = new List<Chapter>();
    public ICollection<Document> Documents { get; set; } = new List<Document>();
}

/// <summary>A chapter/section within a subject.</summary>
public class Chapter : AuditableEntity, ISoftDeletable
{
    public long SubjectId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int OrderIndex { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public long? DeletedBy { get; set; }

    public Subject Subject { get; set; } = null!;
    public ICollection<Document> Documents { get; set; } = new List<Document>();
}
