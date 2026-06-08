using Chatbot.Domain.Common;
using Chatbot.Domain.Enums;

namespace Chatbot.Domain.Entities;

/// <summary>An uploaded course document. The file is stored on disk; this row holds its metadata.</summary>
public class Document : AuditableEntity, ISoftDeletable
{
    public long SubjectId { get; set; }
    public long? ChapterId { get; set; }
    public string Title { get; set; } = string.Empty;

    public string OriginalFileName { get; set; } = string.Empty;
    public string StoredFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public FileType FileType { get; set; }
    public string FileExtension { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string RelativePath { get; set; } = string.Empty;
    public string StorageProvider { get; set; } = "LocalDisk";
    public string Sha256Checksum { get; set; } = string.Empty;
    public int? PageCount { get; set; }

    public DocumentStatus Status { get; set; } = DocumentStatus.Uploaded;
    public DateTime? IndexedAtUtc { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public long? DeletedBy { get; set; }

    public Subject Subject { get; set; } = null!;
    public Chapter? Chapter { get; set; }
    public ICollection<DocumentChunk> Chunks { get; set; } = new List<DocumentChunk>();
    public ICollection<DocumentProcessingJob> ProcessingJobs { get; set; } = new List<DocumentProcessingJob>();
}

/// <summary>Tracks one ingestion run for a document through the parse -> chunk -> embed -> index pipeline.</summary>
public class DocumentProcessingJob : CreatedEntity
{
    public long DocumentId { get; set; }
    public long? ChunkingStrategyId { get; set; }
    public long? EmbeddingModelId { get; set; }

    public ProcessingStage Stage { get; set; } = ProcessingStage.Parse;
    public ProcessingState State { get; set; } = ProcessingState.Queued;
    public int AttemptCount { get; set; }
    public int MaxAttempts { get; set; } = 3;

    public string? HangfireJobId { get; set; }
    public string? Error { get; set; }
    public string? Detail { get; set; }

    public DateTime? StartedAtUtc { get; set; }
    public DateTime? FinishedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }

    public Document Document { get; set; } = null!;
}
