using Chatbot.Application.Common.Exceptions;
using Chatbot.Application.Common.Interfaces;
using Chatbot.Domain.Entities;
using Chatbot.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Chatbot.Application.Features.Experiments;

public sealed record ListTestQuestionsQuery(long? SubjectId) : IRequest<IReadOnlyList<TestQuestionDto>>;

public sealed class ListTestQuestionsQueryHandler(IAppDbContext db)
    : IRequestHandler<ListTestQuestionsQuery, IReadOnlyList<TestQuestionDto>>
{
    public async Task<IReadOnlyList<TestQuestionDto>> Handle(ListTestQuestionsQuery request, CancellationToken ct)
    {
        var query = db.TestQuestions.AsNoTracking();
        if (request.SubjectId is { } subjectId)
        {
            query = query.Where(q => q.SubjectId == subjectId);
        }

        return await query.OrderBy(q => q.ExternalRef).ThenBy(q => q.Id)
            .Select(q => new TestQuestionDto(
                q.Id, q.SubjectId, q.Question, q.GroundTruth, q.ReferenceContext, q.Difficulty, q.ExternalRef))
            .ToListAsync(ct);
    }
}

public sealed record TestQuestionImportItem(
    string Question, string GroundTruth, string? ReferenceContext, string? Difficulty, string? ExternalRef);

public sealed record ImportTestQuestionsCommand(long SubjectId, IReadOnlyList<TestQuestionImportItem> Items)
    : IRequest<int>;

public sealed class ImportTestQuestionsCommandValidator : AbstractValidator<ImportTestQuestionsCommand>
{
    public ImportTestQuestionsCommandValidator()
    {
        RuleFor(x => x.SubjectId).GreaterThan(0);
        RuleFor(x => x.Items).NotEmpty();
    }
}

public sealed class ImportTestQuestionsCommandHandler(IAppDbContext db)
    : IRequestHandler<ImportTestQuestionsCommand, int>
{
    public async Task<int> Handle(ImportTestQuestionsCommand request, CancellationToken ct)
    {
        if (!await db.Subjects.AnyAsync(s => s.Id == request.SubjectId, ct))
        {
            throw new NotFoundException("Không tìm thấy môn học.");
        }

        var existingRefs = await db.TestQuestions
            .Where(q => q.SubjectId == request.SubjectId && q.ExternalRef != null)
            .Select(q => q.ExternalRef!)
            .ToListAsync(ct);
        var existing = existingRefs.ToHashSet();

        var added = 0;
        foreach (var item in request.Items)
        {
            if (item.ExternalRef != null && existing.Contains(item.ExternalRef))
            {
                continue;
            }

            Difficulty? difficulty = Enum.TryParse<Difficulty>(item.Difficulty, ignoreCase: true, out var d) ? d : null;
            db.TestQuestions.Add(new TestQuestion
            {
                SubjectId = request.SubjectId,
                Question = item.Question,
                GroundTruth = item.GroundTruth,
                ReferenceContext = item.ReferenceContext,
                Difficulty = difficulty,
                ExternalRef = item.ExternalRef,
            });
            added++;
        }

        await db.SaveChangesAsync(ct);
        return added;
    }
}
