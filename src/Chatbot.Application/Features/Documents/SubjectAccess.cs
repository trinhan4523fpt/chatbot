using Chatbot.Application.Common.Authorization;
using Chatbot.Application.Common.Exceptions;
using Chatbot.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Chatbot.Application.Features.Documents;

internal static class SubjectAccess
{
    /// <summary>Admins manage any subject; everyone else must be an assigned instructor of it.</summary>
    public static async Task EnsureCanManageAsync(
        IAppDbContext db, ICurrentUser currentUser, long subjectId, CancellationToken ct)
    {
        if (currentUser.Roles.Contains(RoleDefinitions.Admin))
        {
            return;
        }

        var userId = currentUser.UserId;
        var assigned = userId is { } id
            && await db.SubjectInstructors.AnyAsync(si => si.SubjectId == subjectId && si.UserId == id, ct);
        if (!assigned)
        {
            throw new ForbiddenException("Bạn chưa được phân công môn học này nên không thể quản lý tài liệu của nó.");
        }
    }
}
