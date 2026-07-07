using Chatbot.Application.Common.Exceptions;
using Chatbot.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Chatbot.Application.Features.Auth;

public sealed record GetMeQuery(long UserId) : IRequest<CurrentUserDto>;

public sealed class GetMeQueryHandler(IAppDbContext db) : IRequestHandler<GetMeQuery, CurrentUserDto>
{
    public async Task<CurrentUserDto> Handle(GetMeQuery request, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == request.UserId, ct)
            ?? throw new NotFoundException("Không tìm thấy người dùng.");

        var roles = await db.UserRoles.Where(ur => ur.UserId == user.Id)
            .Select(ur => ur.Role.Name).ToListAsync(ct);

        var permissions = await db.UserRoles.Where(ur => ur.UserId == user.Id)
            .SelectMany(ur => ur.Role.RolePermissions)
            .Select(rp => rp.Permission.Code)
            .Distinct()
            .ToListAsync(ct);

        return new CurrentUserDto(user.Id, user.Email, user.FullName, roles, permissions);
    }
}
