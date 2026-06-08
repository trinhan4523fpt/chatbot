using Chatbot.Application.Common.Interfaces;
using Chatbot.Domain.Entities;

namespace Chatbot.Application.Features.Auth;

internal static class AuthTokenHelper
{
    /// <summary>Issues an access + refresh token pair, persisting the refresh token; optionally rotates an old one.</summary>
    public static async Task<TokenResponse> IssueTokensAsync(
        IAppDbContext db,
        IJwtTokenService jwt,
        IClock clock,
        User user,
        IReadOnlyList<string> roles,
        Guid familyId,
        string? ip,
        int refreshDays,
        CancellationToken ct,
        RefreshToken? rotatedFrom = null)
    {
        var access = jwt.CreateAccessToken(
            user.Id, user.Email, user.FullName, user.SecurityStamp, roles, user.MustChangePassword);

        var rawRefresh = jwt.GenerateRefreshToken();
        var newHash = jwt.HashRefreshToken(rawRefresh);

        if (rotatedFrom is not null)
        {
            rotatedFrom.RevokedAtUtc = clock.UtcNow;
            rotatedFrom.RevokedByIp = ip;
            rotatedFrom.ReplacedByTokenHash = newHash;
            rotatedFrom.ReasonRevoked = "rotated";
        }

        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = newHash,
            FamilyId = familyId,
            JwtId = access.JwtId,
            ExpiresAtUtc = clock.UtcNow.AddDays(refreshDays),
            CreatedByIp = ip,
        });

        await db.SaveChangesAsync(ct);
        return new TokenResponse(access.Token, access.ExpiresAtUtc, rawRefresh, user.MustChangePassword);
    }
}
