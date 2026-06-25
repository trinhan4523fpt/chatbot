using Chatbot.Application.Common.Exceptions;
using Chatbot.Application.Common.Interfaces;
using Chatbot.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Chatbot.Application.Features.Auth;

public sealed record LoginCommand(string Email, string Password, string? IpAddress) : IRequest<TokenResponse>;

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(256);
    }
}

public sealed class LoginCommandHandler(
    IAppDbContext db, IJwtTokenService jwt, IPasswordHasher hasher, IClock clock)
    : IRequestHandler<LoginCommand, TokenResponse>
{
    private const string InvalidCredentials = "Email hoặc mật khẩu không đúng.";

    public async Task<TokenResponse> Handle(LoginCommand request, CancellationToken ct)
    {
        var normalized = request.Email.ToUpperInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.NormalizedEmail == normalized, ct);
        var cfg = await db.SystemConfigurations.AsNoTracking().FirstOrDefaultAsync(ct);
        var maxAttempts = cfg?.LockoutMaxAttempts ?? 5;
        var lockoutMinutes = cfg?.LockoutMinutes ?? 15;
        var refreshDays = cfg?.RefreshTokenDays ?? 7;

        if (user is null)
        {
            db.AuditLogs.Add(new AuditLog
            {
                Action = "LoginAttempt", ActorEmail = request.Email, IpAddress = request.IpAddress,
                NewValues = "{\"result\":\"unknown_email\"}",
            });
            await db.SaveChangesAsync(ct);
            throw new UnauthorizedException(InvalidCredentials);
        }

        if (user.LockoutEndUtc is { } until && until > clock.UtcNow)
        {
            throw new UnauthorizedException("Tài khoản đang bị tạm khóa. Vui lòng thử lại sau.");
        }

        if (!user.IsActive)
        {
            throw new UnauthorizedException(InvalidCredentials);
        }

        if (!user.EmailConfirmed)
        {
            throw new UnauthorizedException("Email chưa được xác nhận. Vui lòng xác nhận email trước khi đăng nhập.");
        }

        var verify = hasher.Verify(user.PasswordHash, request.Password);
        if (verify == PasswordVerificationResult.Failed)
        {
            user.AccessFailedCount++;
            if (user.AccessFailedCount >= maxAttempts)
            {
                user.LockoutEndUtc = clock.UtcNow.AddMinutes(lockoutMinutes);
                user.AccessFailedCount = 0;
            }

            db.AuditLogs.Add(new AuditLog
            {
                Action = "LoginFailed", ActorUserId = user.Id, ActorEmail = user.Email,
                TargetUserId = user.Id, IpAddress = request.IpAddress,
            });
            await db.SaveChangesAsync(ct);
            throw new UnauthorizedException(InvalidCredentials);
        }

        if (verify == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = hasher.Hash(request.Password);
        }

        user.AccessFailedCount = 0;
        user.LockoutEndUtc = null;
        user.LastLoginUtc = clock.UtcNow;

        var roles = await db.UserRoles
            .Where(ur => ur.UserId == user.Id)
            .Select(ur => ur.Role.Name)
            .ToListAsync(ct);

        db.AuditLogs.Add(new AuditLog
        {
            Action = "LoginSuccess", ActorUserId = user.Id, ActorEmail = user.Email,
            TargetUserId = user.Id, IpAddress = request.IpAddress,
        });

        return await AuthTokenHelper.IssueTokensAsync(
            db, jwt, clock, user, roles, Guid.NewGuid(), request.IpAddress, refreshDays, ct);
    }
}
