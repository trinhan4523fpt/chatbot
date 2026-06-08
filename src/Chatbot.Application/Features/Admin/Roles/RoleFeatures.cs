using Chatbot.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Chatbot.Application.Features.Admin.Roles;

public sealed record RoleDto(long Id, string Name, string? Description, IReadOnlyList<string> Permissions);

public sealed record ListRolesQuery : IRequest<IReadOnlyList<RoleDto>>;

public sealed class ListRolesQueryHandler(IAppDbContext db) : IRequestHandler<ListRolesQuery, IReadOnlyList<RoleDto>>
{
    public async Task<IReadOnlyList<RoleDto>> Handle(ListRolesQuery request, CancellationToken ct) =>
        await db.Roles.AsNoTracking()
            .OrderBy(r => r.Name)
            .Select(r => new RoleDto(
                r.Id, r.Name, r.Description,
                r.RolePermissions.Select(rp => rp.Permission.Code).OrderBy(c => c).ToList()))
            .ToListAsync(ct);
}
