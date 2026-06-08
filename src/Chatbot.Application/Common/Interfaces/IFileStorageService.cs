namespace Chatbot.Application.Common.Interfaces;

/// <summary>A file streamed to a temporary location, with its computed hash/size and header bytes.</summary>
public sealed record StagedFile(string Token, string Sha256, long SizeBytes, byte[] Header);

/// <summary>Static-file storage on disk (outside the web root). Two-phase: stage -> commit.</summary>
public interface IFileStorageService
{
    /// <summary>Streams content to a temp file, computing SHA-256 (lowercase hex) and size; returns header bytes too.</summary>
    Task<StagedFile> StageAsync(Stream content, CancellationToken ct = default);

    /// <summary>Moves a staged file to its permanent path; returns the stored relative path.</summary>
    Task<string> CommitAsync(string token, long subjectId, long documentId, string storedFileName, CancellationToken ct = default);

    /// <summary>Deletes a staged temp file (validation failed / aborted).</summary>
    Task DiscardAsync(string token, CancellationToken ct = default);

    /// <summary>Deletes all stored files for a document.</summary>
    Task DeleteDocumentDirectoryAsync(long subjectId, long documentId, CancellationToken ct = default);

    /// <summary>Resolves a stored relative path to an absolute path, guarding against path traversal.</summary>
    string ResolvePhysicalPath(string relativePath);
}
