using Chatbot.Application.Common.Exceptions;
using Chatbot.Application.Common.Interfaces;
using Chatbot.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Chatbot.Application.Features.Catalog;

public sealed record SubjectInstructorDto(long UserId, string FullName, string Email);

// ---- Assign ---------------------------------------------------------------------
public sealed record AssignInstructorCommand(long SubjectId, long UserId) : IRequest<Unit>;

public sealed class AssignInstructorCommandHandler(IAppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<AssignInstructorCommand, Unit>
{
    public async Task<Unit> Handle(AssignInstructorCommand request, CancellationToken ct)
    {
        if (!await db.Subjects.AnyAsync(s => s.Id == request.SubjectId, ct))
        {
            throw new NotFoundException("Không tìm thấy môn học.");
        }

        if (!await db.Users.AnyAsync(u => u.Id == request.UserId, ct))
        {
            throw new NotFoundException("Không tìm thấy người dùng.");
        }

        // One instructor per subject: reject a different instructor; same instructor is idempotent.
        var current = await db.SubjectInstructors
            .FirstOrDefaultAsync(si => si.SubjectId == request.SubjectId, ct);
        if (current is not null)
        {
            if (current.UserId == request.UserId)
            {
                return Unit.Value; // already assigned
            }

            throw new ConflictException(
                "Môn học này đã có giảng viên phụ trách. Hãy gỡ giảng viên hiện tại trước khi gán người khác.");
        }

        db.SubjectInstructors.Add(new SubjectInstructor
        {
            SubjectId = request.SubjectId, UserId = request.UserId, AssignedBy = currentUser.UserId,
        });
        db.AuditLogs.Add(new AuditLog
        {
            Action = "InstructorAssigned", ActorUserId = currentUser.UserId, ActorEmail = currentUser.Email,
            TargetUserId = request.UserId, TargetType = "Subject", TargetId = request.SubjectId.ToString(),
        });
        await db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

// ---- Unassign -------------------------------------------------------------------
public sealed record UnassignInstructorCommand(long SubjectId, long UserId) : IRequest<Unit>;

public sealed class UnassignInstructorCommandHandler(IAppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<UnassignInstructorCommand, Unit>
{
    public async Task<Unit> Handle(UnassignInstructorCommand request, CancellationToken ct)
    {
        var link = await db.SubjectInstructors
            .FirstOrDefaultAsync(si => si.SubjectId == request.SubjectId && si.UserId == request.UserId, ct);
        if (link is null)
        {
            return Unit.Value;
        }

        db.SubjectInstructors.Remove(link);
        db.AuditLogs.Add(new AuditLog
        {
            Action = "InstructorUnassigned", ActorUserId = currentUser.UserId, ActorEmail = currentUser.Email,
            TargetUserId = request.UserId, TargetType = "Subject", TargetId = request.SubjectId.ToString(),
        });
        await db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

// ---- List instructors of a subject ---------------------------------------------
public sealed record ListSubjectInstructorsQuery(long SubjectId) : IRequest<IReadOnlyList<SubjectInstructorDto>>;

public sealed class ListSubjectInstructorsQueryHandler(IAppDbContext db)
    : IRequestHandler<ListSubjectInstructorsQuery, IReadOnlyList<SubjectInstructorDto>>
{
    public async Task<IReadOnlyList<SubjectInstructorDto>> Handle(ListSubjectInstructorsQuery request, CancellationToken ct) =>
        await db.SubjectInstructors.AsNoTracking()
            .Where(si => si.SubjectId == request.SubjectId)
            .OrderBy(si => si.User.FullName)
            .Select(si => new SubjectInstructorDto(si.UserId, si.User.FullName, si.User.Email))
            .ToListAsync(ct);
}
