using Chatbot.Application.Common.Exceptions;
using Chatbot.Application.Common.Interfaces;
using Chatbot.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Chatbot.Application.Features.Catalog;

public sealed record SubjectDto(long Id, string Code, string Name, string? Description, int ChapterCount, int DocumentCount);

public sealed record ChapterDto(long Id, long SubjectId, string Title, int OrderIndex);

// ---- Subjects ------------------------------------------------------------------
public sealed record ListSubjectsQuery : IRequest<IReadOnlyList<SubjectDto>>;

public sealed class ListSubjectsQueryHandler(IAppDbContext db) : IRequestHandler<ListSubjectsQuery, IReadOnlyList<SubjectDto>>
{
    public async Task<IReadOnlyList<SubjectDto>> Handle(ListSubjectsQuery request, CancellationToken ct) =>
        await db.Subjects.AsNoTracking().OrderBy(s => s.Code)
            .Select(s => new SubjectDto(s.Id, s.Code, s.Name, s.Description, s.Chapters.Count, s.Documents.Count))
            .ToListAsync(ct);
}

public sealed record CreateSubjectCommand(string Code, string Name, string? Description) : IRequest<long>;

public sealed class CreateSubjectCommandValidator : AbstractValidator<CreateSubjectCommand>
{
    public CreateSubjectCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
    }
}

public sealed class CreateSubjectCommandHandler(IAppDbContext db) : IRequestHandler<CreateSubjectCommand, long>
{
    public async Task<long> Handle(CreateSubjectCommand request, CancellationToken ct)
    {
        if (await db.Subjects.AnyAsync(s => s.Code == request.Code, ct))
        {
            throw new ConflictException($"Mã môn học '{request.Code}' đã tồn tại.");
        }

        var subject = new Subject { Code = request.Code, Name = request.Name, Description = request.Description };
        db.Subjects.Add(subject);
        await db.SaveChangesAsync(ct);
        return subject.Id;
    }
}

public sealed record UpdateSubjectCommand(long Id, string Name, string? Description) : IRequest<Unit>;

public sealed class UpdateSubjectCommandValidator : AbstractValidator<UpdateSubjectCommand>
{
    public UpdateSubjectCommandValidator() => RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
}

public sealed class UpdateSubjectCommandHandler(IAppDbContext db) : IRequestHandler<UpdateSubjectCommand, Unit>
{
    public async Task<Unit> Handle(UpdateSubjectCommand request, CancellationToken ct)
    {
        var subject = await db.Subjects.FirstOrDefaultAsync(s => s.Id == request.Id, ct)
            ?? throw new NotFoundException("Không tìm thấy môn học.");
        subject.Name = request.Name;
        subject.Description = request.Description;
        await db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

// ---- Chapters ------------------------------------------------------------------
public sealed record ListChaptersQuery(long SubjectId) : IRequest<IReadOnlyList<ChapterDto>>;

public sealed class ListChaptersQueryHandler(IAppDbContext db) : IRequestHandler<ListChaptersQuery, IReadOnlyList<ChapterDto>>
{
    public async Task<IReadOnlyList<ChapterDto>> Handle(ListChaptersQuery request, CancellationToken ct) =>
        await db.Chapters.AsNoTracking()
            .Where(c => c.SubjectId == request.SubjectId)
            .OrderBy(c => c.OrderIndex)
            .Select(c => new ChapterDto(c.Id, c.SubjectId, c.Title, c.OrderIndex))
            .ToListAsync(ct);
}

public sealed record CreateChapterCommand(long SubjectId, string Title, int OrderIndex) : IRequest<long>;

public sealed class CreateChapterCommandValidator : AbstractValidator<CreateChapterCommand>
{
    public CreateChapterCommandValidator() => RuleFor(x => x.Title).NotEmpty().MaximumLength(255);
}

public sealed class CreateChapterCommandHandler(IAppDbContext db) : IRequestHandler<CreateChapterCommand, long>
{
    public async Task<long> Handle(CreateChapterCommand request, CancellationToken ct)
    {
        if (!await db.Subjects.AnyAsync(s => s.Id == request.SubjectId, ct))
        {
            throw new NotFoundException("Không tìm thấy môn học.");
        }

        var chapter = new Chapter { SubjectId = request.SubjectId, Title = request.Title, OrderIndex = request.OrderIndex };
        db.Chapters.Add(chapter);
        await db.SaveChangesAsync(ct);
        return chapter.Id;
    }
}
