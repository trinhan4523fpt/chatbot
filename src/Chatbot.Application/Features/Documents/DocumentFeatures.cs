using Chatbot.Application.Common.Authorization;
using Chatbot.Application.Common.Exceptions;
using Chatbot.Application.Common.Files;
using Chatbot.Application.Common.Interfaces;
using Chatbot.Application.Common.Models;
using Chatbot.Domain.Entities;
using Chatbot.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Chatbot.Application.Features.Documents;

public sealed record DocumentDto(
    long Id, long SubjectId, long? ChapterId, string Title, string OriginalFileName,
    FileType FileType, long SizeBytes, DocumentStatus Status, int? PageCount,
    DateTime? IndexedAtUtc, DateTime CreatedAtUtc, int ChunkCount);

public sealed record DocumentChunkDto(
    long Id, long ChunkingStrategyId, int ChunkIndex, int? PageNumber, int? TokenCount, string Content);

public sealed record DocumentDownloadInfo(string PhysicalPath, string ContentType, string OriginalFileName);

internal static class DocumentMapping
{
    public static DocumentDto Map(Document d, int chunkCount = 0) => new(
        d.Id, d.SubjectId, d.ChapterId, d.Title, d.OriginalFileName, d.FileType, d.SizeBytes,
        d.Status, d.PageCount, d.IndexedAtUtc, d.CreatedAtUtc, chunkCount);
}

// ---- Upload --------------------------------------------------------------------
public sealed record UploadDocumentCommand(
    Stream Content, string OriginalFileName, string ContentType, long SubjectId, long? ChapterId, string? Title)
    : IRequest<DocumentDto>;

public sealed class UploadDocumentCommandValidator : AbstractValidator<UploadDocumentCommand>
{
    public UploadDocumentCommandValidator()
    {
        RuleFor(x => x.OriginalFileName).NotEmpty().MaximumLength(260);
        RuleFor(x => x.SubjectId).GreaterThan(0);
        RuleFor(x => x.Title).MaximumLength(300);
    }
}

public sealed class UploadDocumentCommandHandler(
    IAppDbContext db, IFileStorageService storage, ICurrentUser currentUser, IJobScheduler jobScheduler)
    : IRequestHandler<UploadDocumentCommand, DocumentDto>
{
    public async Task<DocumentDto> Handle(UploadDocumentCommand request, CancellationToken ct)
    {
        var subject = await db.Subjects.FirstOrDefaultAsync(s => s.Id == request.SubjectId, ct)
            ?? throw new NotFoundException("Không tìm thấy môn học.");

        await SubjectAccess.EnsureCanManageAsync(db, currentUser, subject.Id, ct);

        if (request.ChapterId is { } chapterId &&
            !await db.Chapters.AnyAsync(c => c.Id == chapterId && c.SubjectId == subject.Id, ct))
        {
            throw new NotFoundException("Không tìm thấy chương trong môn học.");
        }

        var cfg = await db.SystemConfigurations.AsNoTracking().FirstOrDefaultAsync(ct);
        var maxBytes = cfg?.MaxUploadBytes ?? 52_428_800;

        var staged = await storage.StageAsync(request.Content, ct);
        try
        {
            if (staged.SizeBytes == 0)
            {
                throw Invalid("Tệp rỗng.");
            }

            if (staged.SizeBytes > maxBytes)
            {
                throw Invalid($"Tệp vượt quá kích thước tối đa ({maxBytes / (1024 * 1024)} MB).");
            }

            if (!FileTypePolicy.TryValidate(request.OriginalFileName, staged.Header, out var fileType, out var ext, out var error))
            {
                throw Invalid(error);
            }

            if (await db.Documents.AnyAsync(d => d.SubjectId == subject.Id && d.Sha256Checksum == staged.Sha256, ct))
            {
                throw new ConflictException("Tài liệu trùng nội dung đã tồn tại trong môn học này.");
            }

            var storedFileName = $"{Guid.NewGuid():N}{ext}";
            var title = string.IsNullOrWhiteSpace(request.Title)
                ? Path.GetFileNameWithoutExtension(request.OriginalFileName)
                : request.Title;

            var document = new Document
            {
                SubjectId = subject.Id,
                ChapterId = request.ChapterId,
                Title = title,
                OriginalFileName = request.OriginalFileName,
                StoredFileName = storedFileName,
                ContentType = string.IsNullOrWhiteSpace(request.ContentType) ? "application/octet-stream" : request.ContentType,
                FileType = fileType,
                FileExtension = ext,
                SizeBytes = staged.SizeBytes,
                Sha256Checksum = staged.Sha256,
                RelativePath = "(pending)",
                Status = DocumentStatus.Uploaded,
            };
            db.Documents.Add(document);
            await db.SaveChangesAsync(ct);

            document.RelativePath = await storage.CommitAsync(staged.Token, subject.Id, document.Id, storedFileName, ct);

            var processingJob = new DocumentProcessingJob
            {
                DocumentId = document.Id, State = ProcessingState.Queued, Stage = ProcessingStage.Parse,
            };
            db.DocumentProcessingJobs.Add(processingJob);
            db.AuditLogs.Add(new AuditLog
            {
                Action = "DocumentUploaded", ActorUserId = currentUser.UserId, ActorEmail = currentUser.Email,
                TargetType = "Document", TargetId = document.Id.ToString(),
            });
            await db.SaveChangesAsync(ct);

            processingJob.HangfireJobId = jobScheduler.EnqueueIngest(document.Id);
            await db.SaveChangesAsync(ct);

            return DocumentMapping.Map(document);
        }
        catch
        {
            await storage.DiscardAsync(staged.Token, ct);
            throw;
        }
    }

    private static Common.Exceptions.ValidationException Invalid(string message) =>
        new(new Dictionary<string, string[]> { ["file"] = [message] });
}

// ---- List ----------------------------------------------------------------------
public sealed record ListDocumentsQuery(long? SubjectId, DocumentStatus? Status, int Page = 1, int PageSize = 20)
    : IRequest<PagedResult<DocumentDto>>;

public sealed class ListDocumentsQueryValidator : AbstractValidator<ListDocumentsQuery>
{
    public ListDocumentsQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}

public sealed class ListDocumentsQueryHandler(IAppDbContext db)
    : IRequestHandler<ListDocumentsQuery, PagedResult<DocumentDto>>
{
    public async Task<PagedResult<DocumentDto>> Handle(ListDocumentsQuery request, CancellationToken ct)
    {
        var query = db.Documents.AsNoTracking();
        if (request.SubjectId is { } subjectId)
        {
            query = query.Where(d => d.SubjectId == subjectId);
        }

        if (request.Status is { } status)
        {
            query = query.Where(d => d.Status == status);
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(d => d.CreatedAtUtc).ThenBy(d => d.Id)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize)
            .Select(d => new DocumentDto(
                d.Id, d.SubjectId, d.ChapterId, d.Title, d.OriginalFileName, d.FileType, d.SizeBytes,
                d.Status, d.PageCount, d.IndexedAtUtc, d.CreatedAtUtc, d.Chunks.Count))
            .ToListAsync(ct);

        return new PagedResult<DocumentDto>(items, request.Page, request.PageSize, total);
    }
}

// ---- Get -----------------------------------------------------------------------
public sealed record GetDocumentQuery(long Id) : IRequest<DocumentDto>;

public sealed class GetDocumentQueryHandler(IAppDbContext db) : IRequestHandler<GetDocumentQuery, DocumentDto>
{
    public async Task<DocumentDto> Handle(GetDocumentQuery request, CancellationToken ct)
    {
        var document = await db.Documents.AsNoTracking().FirstOrDefaultAsync(d => d.Id == request.Id, ct)
            ?? throw new NotFoundException("Không tìm thấy tài liệu.");
        var chunkCount = await db.DocumentChunks.CountAsync(c => c.DocumentId == request.Id, ct);
        return DocumentMapping.Map(document, chunkCount);
    }
}

// ---- View chunks ---------------------------------------------------------------
public sealed record GetDocumentChunksQuery(long DocumentId, int Page = 1, int PageSize = 50)
    : IRequest<PagedResult<DocumentChunkDto>>;

public sealed class GetDocumentChunksQueryValidator : AbstractValidator<GetDocumentChunksQuery>
{
    public GetDocumentChunksQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
    }
}

public sealed class GetDocumentChunksQueryHandler(IAppDbContext db)
    : IRequestHandler<GetDocumentChunksQuery, PagedResult<DocumentChunkDto>>
{
    public async Task<PagedResult<DocumentChunkDto>> Handle(GetDocumentChunksQuery request, CancellationToken ct)
    {
        if (!await db.Documents.AnyAsync(d => d.Id == request.DocumentId, ct))
        {
            throw new NotFoundException("Không tìm thấy tài liệu.");
        }

        var query = db.DocumentChunks.AsNoTracking().Where(c => c.DocumentId == request.DocumentId);
        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(c => c.ChunkingStrategyId).ThenBy(c => c.ChunkIndex)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize)
            .Select(c => new DocumentChunkDto(
                c.Id, c.ChunkingStrategyId, c.ChunkIndex, c.PageNumber, c.TokenCount, c.Content))
            .ToListAsync(ct);

        return new PagedResult<DocumentChunkDto>(items, request.Page, request.PageSize, total);
    }
}

// ---- Download (subject-scoped) -------------------------------------------------
public sealed record DownloadDocumentQuery(long Id) : IRequest<DocumentDownloadInfo>;

public sealed class DownloadDocumentQueryHandler(IAppDbContext db, IFileStorageService storage, ICurrentUser currentUser)
    : IRequestHandler<DownloadDocumentQuery, DocumentDownloadInfo>
{
    public async Task<DocumentDownloadInfo> Handle(DownloadDocumentQuery request, CancellationToken ct)
    {
        var document = await db.Documents.AsNoTracking().FirstOrDefaultAsync(d => d.Id == request.Id, ct)
            ?? throw new NotFoundException("Không tìm thấy tài liệu.");

        var privileged = currentUser.Roles.Contains(RoleDefinitions.Admin)
                         || currentUser.Roles.Contains(RoleDefinitions.Researcher);
        if (!privileged)
        {
            var enrolled = currentUser.UserId is { } uid &&
                await db.UserSubjects.AnyAsync(us => us.UserId == uid && us.SubjectId == document.SubjectId, ct);
            if (!enrolled)
            {
                throw new ForbiddenException("Bạn không có quyền truy cập tài liệu của môn học này.");
            }
        }

        var physicalPath = storage.ResolvePhysicalPath(document.RelativePath);
        return new DocumentDownloadInfo(physicalPath, document.ContentType, document.OriginalFileName);
    }
}

// ---- Delete --------------------------------------------------------------------
public sealed record DeleteDocumentCommand(long Id) : IRequest<Unit>;

public sealed class DeleteDocumentCommandHandler(
    IAppDbContext db, IFileStorageService storage, IVectorStore vectors, ICurrentUser currentUser)
    : IRequestHandler<DeleteDocumentCommand, Unit>
{
    public async Task<Unit> Handle(DeleteDocumentCommand request, CancellationToken ct)
    {
        var document = await db.Documents.FirstOrDefaultAsync(d => d.Id == request.Id, ct)
            ?? throw new NotFoundException("Không tìm thấy tài liệu.");

        await SubjectAccess.EnsureCanManageAsync(db, currentUser, document.SubjectId, ct);

        // Capture Qdrant collections holding this document's vectors before removing the linkage.
        var collections = await db.ChunkEmbeddings
            .Where(e => e.Chunk.DocumentId == document.Id)
            .Select(e => e.VectorCollection)
            .Distinct()
            .ToListAsync(ct);

        // Soft-delete the document (keeps transcripts/citations via DocumentTitle snapshot),
        // hard-delete its chunks (cascades embeddings), remove vectors + files.
        var chunks = await db.DocumentChunks.Where(c => c.DocumentId == document.Id).ToListAsync(ct);
        db.DocumentChunks.RemoveRange(chunks);
        db.Documents.Remove(document);
        db.AuditLogs.Add(new AuditLog
        {
            Action = "DocumentDeleted", ActorUserId = currentUser.UserId, ActorEmail = currentUser.Email,
            TargetType = "Document", TargetId = document.Id.ToString(),
        });
        await db.SaveChangesAsync(ct);

        foreach (var collection in collections)
        {
            await vectors.DeleteByDocumentAsync(collection, document.Id, ct);
        }

        await storage.DeleteDocumentDirectoryAsync(document.SubjectId, document.Id, ct);
        return Unit.Value;
    }
}

// ---- Reindex -------------------------------------------------------------------
public sealed record ReindexDocumentCommand(long Id) : IRequest<Unit>;

public sealed class ReindexDocumentCommandHandler(IAppDbContext db, IJobScheduler jobScheduler, ICurrentUser currentUser)
    : IRequestHandler<ReindexDocumentCommand, Unit>
{
    public async Task<Unit> Handle(ReindexDocumentCommand request, CancellationToken ct)
    {
        var document = await db.Documents.FirstOrDefaultAsync(d => d.Id == request.Id, ct)
            ?? throw new NotFoundException("Không tìm thấy tài liệu.");

        await SubjectAccess.EnsureCanManageAsync(db, currentUser, document.SubjectId, ct);

        var hasActiveJob = await db.DocumentProcessingJobs.AnyAsync(
            j => j.DocumentId == document.Id &&
                 (j.State == Domain.Enums.ProcessingState.Queued || j.State == Domain.Enums.ProcessingState.Running), ct);
        if (hasActiveJob)
        {
            throw new ConflictException("Tài liệu đang được xử lý. Vui lòng đợi hoàn tất.");
        }

        document.Status = DocumentStatus.Uploaded;
        var job = new DocumentProcessingJob
        {
            DocumentId = document.Id, State = ProcessingState.Queued, Stage = ProcessingStage.Parse,
        };
        db.DocumentProcessingJobs.Add(job);
        db.AuditLogs.Add(new AuditLog
        {
            Action = "DocumentReindexRequested", ActorUserId = currentUser.UserId, ActorEmail = currentUser.Email,
            TargetType = "Document", TargetId = document.Id.ToString(),
        });
        await db.SaveChangesAsync(ct);

        job.HangfireJobId = jobScheduler.EnqueueIngest(document.Id);
        await db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

// ---- Processing status ---------------------------------------------------------
public sealed record DocumentStatusDto(
    long DocumentId, DocumentStatus DocumentStatus, ProcessingStage? Stage, ProcessingState? State,
    int AttemptCount, string? Error, DateTime? StartedAtUtc, DateTime? FinishedAtUtc);

public sealed record GetDocumentStatusQuery(long Id) : IRequest<DocumentStatusDto>;

public sealed class GetDocumentStatusQueryHandler(IAppDbContext db) : IRequestHandler<GetDocumentStatusQuery, DocumentStatusDto>
{
    public async Task<DocumentStatusDto> Handle(GetDocumentStatusQuery request, CancellationToken ct)
    {
        var document = await db.Documents.AsNoTracking().FirstOrDefaultAsync(d => d.Id == request.Id, ct)
            ?? throw new NotFoundException("Không tìm thấy tài liệu.");

        var job = await db.DocumentProcessingJobs.AsNoTracking()
            .Where(j => j.DocumentId == request.Id)
            .OrderByDescending(j => j.Id)
            .FirstOrDefaultAsync(ct);

        return new DocumentStatusDto(
            document.Id, document.Status, job?.Stage, job?.State, job?.AttemptCount ?? 0,
            job?.Error, job?.StartedAtUtc, job?.FinishedAtUtc);
    }
}
