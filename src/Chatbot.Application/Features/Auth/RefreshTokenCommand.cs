using Chatbot.Application.Common.Exceptions;
using Chatbot.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Chatbot.Application.Features.Auth;

public sealed record RefreshTokenCommand(string RefreshToken, string? IpAddress) : IRequest<TokenResponse>;

public sealed class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator() => RuleFor(x => x.RefreshToken).NotEmpty();
}

public sealed class RefreshTokenCommandHandler(IAppDbContext db, IJwtTokenService jwt, IClock clock)
    : IRequestHandler<RefreshTokenCommand, TokenResponse>
{
    private const string Invalid = "Phiên đăng nhập không hợp lệ. Vui lòng đăng nhập lại.";

    public async Task<TokenResponse> Handle(RefreshTokenCommand request, CancellationToken ct)
    {
        var hash = jwt.HashRefreshToken(request.RefreshToken);
        var token = await db.RefreshTokens.Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == hash, ct);

        if (token is null)
        {
            throw new UnauthorizedException(Invalid);
        }

        // Reuse of an already-revoked token => revoke the entire family.
        if (token.RevokedAtUtc is not null)
        {
            var family = await db.RefreshTokens
                .Where(t => t.FamilyId == token.FamilyId && t.RevokedAtUtc == null)
                .ToListAsync(ct);
            foreach (var t in family)
            {
                t.RevokedAtUtc = clock.UtcNow;
                t.RevokedByIp = request.IpAddress;
                t.ReasonRevoked = "reuse_detected";
            }

            await db.SaveChangesAsync(ct);
            throw new UnauthorizedException(Invalid);
        }

        if (token.ExpiresAtUtc <= clock.UtcNow)
        {
            throw new UnauthorizedException(Invalid);
        }

        var user = token.User;
        if (user is null || !user.IsActive)
        {
            throw new UnauthorizedException(Invalid);
        }

        var cfg = await db.SystemConfigurations.AsNoTracking().FirstOrDefaultAsync(ct);
        var refreshDays = cfg?.RefreshTokenDays ?? 7;

        var roles = await db.UserRoles
            .Where(ur => ur.UserId == user.Id)
            .Select(ur => ur.Role.Name)
            .ToListAsync(ct);

        return await AuthTokenHelper.IssueTokensAsync(
            db, jwt, clock, user, roles, token.FamilyId, request.IpAddress, refreshDays, ct, rotatedFrom: token);
    }
}
