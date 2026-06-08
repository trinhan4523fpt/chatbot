using Chatbot.Application.Common.Authorization;
using Chatbot.Application.Common.Exceptions;
using Chatbot.Application.Common.Interfaces;
using Chatbot.Application.Common.Models;
using Chatbot.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Chatbot.Application.Features.Admin.Users;

public sealed record AdminUserDto(
    long Id, string Email, string FullName, bool IsActive, bool MustChangePassword,
    DateTime? LastLoginUtc, IReadOnlyList<string> Roles);

// ---- List ----------------------------------------------------------------------
public sealed record ListUsersQuery(int Page = 1, int PageSize = 20, string? Search = null)
    : IRequest<PagedResult<AdminUserDto>>;

public sealed class ListUsersQueryValidator : AbstractValidator<ListUsersQuery>
{
    public ListUsersQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}

public sealed class ListUsersQueryHandler(IAppDbContext db) : IRequestHandler<ListUsersQuery, PagedResult<AdminUserDto>>
{
    public async Task<PagedResult<AdminUserDto>> Handle(ListUsersQuery request, CancellationToken ct)
    {
        var query = db.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var s = request.Search.Trim();
            query = query.Where(u => u.Email.Contains(s) || u.FullName.Contains(s));
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(u => u.Email).ThenBy(u => u.Id)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize)
            .Select(u => new AdminUserDto(
                u.Id, u.Email, u.FullName, u.IsActive, u.MustChangePassword, u.LastLoginUtc,
                u.UserRoles.Select(ur => ur.Role.Name).ToList()))
            .ToListAsync(ct);

        return new PagedResult<AdminUserDto>(items, request.Page, request.PageSize, total);
    }
}

// ---- Create --------------------------------------------------------------------
public sealed record CreateUserCommand(string Email, string FullName, string Password, IReadOnlyList<string> Roles)
    : IRequest<long>;

public sealed class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8).MaximumLength(256);
        RuleFor(x => x.Roles).NotNull();
    }
}

public sealed class CreateUserCommandHandler(IAppDbContext db, IPasswordHasher hasher, ICurrentUser currentUser)
    : IRequestHandler<CreateUserCommand, long>
{
    public async Task<long> Handle(CreateUserCommand request, CancellationToken ct)
    {
        var normalized = request.Email.ToUpperInvariant();
        if (await db.Users.AnyAsync(u => u.NormalizedEmail == normalized, ct))
        {
            throw new ConflictException("Email đã tồn tại.");
        }

        var roleNames = request.Roles.Count == 0 ? [RoleDefinitions.Student] : request.Roles;
        var roles = await db.Roles.Where(r => roleNames.Contains(r.Name)).ToListAsync(ct);
        if (roles.Count != roleNames.Distinct().Count())
        {
            throw new Common.Exceptions.ValidationException(new Dictionary<string, string[]>
            {
                ["roles"] = ["Một hoặc nhiều vai trò không hợp lệ."],
            });
        }

        var user = new User
        {
            Email = request.Email,
            NormalizedEmail = normalized,
            FullName = request.FullName,
            PasswordHash = hasher.Hash(request.Password),
            IsActive = true,
            MustChangePassword = true,
            EmailConfirmed = true,
            UserRoles = [.. roles.Select(r => new UserRole { RoleId = r.Id })],
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        db.AuditLogs.Add(new AuditLog
        {
            Action = "UserCreated", ActorUserId = currentUser.UserId, ActorEmail = currentUser.Email,
            TargetUserId = user.Id, TargetType = "User", TargetId = user.Id.ToString(),
        });
        await db.SaveChangesAsync(ct);
        return user.Id;
    }
}

// ---- Assign roles --------------------------------------------------------------
public sealed record AssignRolesCommand(long UserId, IReadOnlyList<string> Roles) : IRequest<Unit>;

public sealed class AssignRolesCommandHandler(IAppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<AssignRolesCommand, Unit>
{
    public async Task<Unit> Handle(AssignRolesCommand request, CancellationToken ct)
    {
        var user = await db.Users.Include(u => u.UserRoles)
            .FirstOrDefaultAsync(u => u.Id == request.UserId, ct)
            ?? throw new NotFoundException("Không tìm thấy người dùng.");

        var roles = await db.Roles.Where(r => request.Roles.Contains(r.Name)).ToListAsync(ct);
        if (roles.Count != request.Roles.Distinct().Count())
        {
            throw new Common.Exceptions.ValidationException(new Dictionary<string, string[]>
            {
                ["roles"] = ["Một hoặc nhiều vai trò không hợp lệ."],
            });
        }

        var isCurrentlyAdmin = user.UserRoles.Any(ur =>
            db.Roles.Any(r => r.Id == ur.RoleId && r.Name == RoleDefinitions.Admin));
        var willBeAdmin = roles.Any(r => r.Name == RoleDefinitions.Admin);
        if (isCurrentlyAdmin && !willBeAdmin)
        {
            var otherActiveAdmins = await db.UserRoles.CountAsync(
                ur => ur.Role.Name == RoleDefinitions.Admin && ur.UserId != user.Id && ur.User.IsActive, ct);
            if (otherActiveAdmins == 0)
            {
                throw new BusinessRuleException("Phải còn ít nhất một quản trị viên đang hoạt động.");
            }
        }

        db.UserRoles.RemoveRange(user.UserRoles);
        foreach (var role in roles)
        {
            db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });
        }

        user.SecurityStamp = Guid.NewGuid().ToString("N"); // role change -> revoke live tokens within SLA
        db.AuditLogs.Add(new AuditLog
        {
            Action = "UserRolesChanged", ActorUserId = currentUser.UserId, ActorEmail = currentUser.Email,
            TargetUserId = user.Id, TargetType = "User", TargetId = user.Id.ToString(),
            NewValues = System.Text.Json.JsonSerializer.Serialize(request.Roles),
        });

        await db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

// ---- Reset password (admin) ----------------------------------------------------
public sealed record ResetUserPasswordCommand(long UserId, string NewPassword) : IRequest<Unit>;

public sealed class ResetUserPasswordCommandValidator : AbstractValidator<ResetUserPasswordCommand>
{
    public ResetUserPasswordCommandValidator() =>
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(8).MaximumLength(256);
}

public sealed class ResetUserPasswordCommandHandler(
    IAppDbContext db, IPasswordHasher hasher, IClock clock, ICurrentUser currentUser)
    : IRequestHandler<ResetUserPasswordCommand, Unit>
{
    public async Task<Unit> Handle(ResetUserPasswordCommand request, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == request.UserId, ct)
            ?? throw new NotFoundException("Không tìm thấy người dùng.");

        user.PasswordHash = hasher.Hash(request.NewPassword);
        user.MustChangePassword = true;
        user.SecurityStamp = Guid.NewGuid().ToString("N");

        var active = await db.RefreshTokens
            .Where(t => t.UserId == user.Id && t.RevokedAtUtc == null).ToListAsync(ct);
        foreach (var token in active)
        {
            token.RevokedAtUtc = clock.UtcNow;
            token.ReasonRevoked = "admin_reset";
        }

        db.AuditLogs.Add(new AuditLog
        {
            Action = "UserPasswordReset", ActorUserId = currentUser.UserId, ActorEmail = currentUser.Email,
            TargetUserId = user.Id, TargetType = "User", TargetId = user.Id.ToString(),
        });
        await db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

// ---- Activate / deactivate -----------------------------------------------------
public sealed record SetUserActiveCommand(long UserId, bool IsActive) : IRequest<Unit>;

public sealed class SetUserActiveCommandHandler(IAppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<SetUserActiveCommand, Unit>
{
    public async Task<Unit> Handle(SetUserActiveCommand request, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == request.UserId, ct)
            ?? throw new NotFoundException("Không tìm thấy người dùng.");

        if (!request.IsActive)
        {
            var isAdmin = await db.UserRoles
                .AnyAsync(ur => ur.UserId == user.Id && ur.Role.Name == RoleDefinitions.Admin, ct);
            if (isAdmin)
            {
                var otherActiveAdmins = await db.UserRoles.CountAsync(
                    ur => ur.Role.Name == RoleDefinitions.Admin && ur.UserId != user.Id && ur.User.IsActive, ct);
                if (otherActiveAdmins == 0)
                {
                    throw new BusinessRuleException("Phải còn ít nhất một quản trị viên đang hoạt động.");
                }
            }
        }

        user.IsActive = request.IsActive;
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        db.AuditLogs.Add(new AuditLog
        {
            Action = request.IsActive ? "UserActivated" : "UserDeactivated",
            ActorUserId = currentUser.UserId, ActorEmail = currentUser.Email,
            TargetUserId = user.Id, TargetType = "User", TargetId = user.Id.ToString(),
        });
        await db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
