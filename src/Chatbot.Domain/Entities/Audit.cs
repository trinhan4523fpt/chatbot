using Chatbot.Domain.Common;
using Chatbot.Domain.Enums;

namespace Chatbot.Domain.Entities;

/// <summary>Append-only security/audit trail. The SQL login is granted INSERT/SELECT only.</summary>
public class AuditLog : CreatedEntity
{
    public long? ActorUserId { get; set; }
    public string? ActorEmail { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? TargetType { get; set; }
    public string? TargetId { get; set; }
    public long? TargetUserId { get; set; }
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}

/// <summary>Transactional outbox for cross-system actions (e.g. Qdrant upsert/delete) consistent with the DB write.</summary>
public class IntegrationOutbox : CreatedEntity
{
    public OutboxEventType EventType { get; set; }
    public string Payload { get; set; } = string.Empty;
    public OutboxStatus Status { get; set; } = OutboxStatus.Pending;
    public int AttemptCount { get; set; }
    public DateTime AvailableAtUtc { get; set; }
    public DateTime? ProcessedAtUtc { get; set; }
    public string? Error { get; set; }
}
