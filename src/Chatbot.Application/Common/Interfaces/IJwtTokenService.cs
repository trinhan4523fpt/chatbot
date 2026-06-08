namespace Chatbot.Application.Common.Interfaces;

public sealed record AccessToken(string Token, DateTime ExpiresAtUtc, string JwtId);

public interface IJwtTokenService
{
    AccessToken CreateAccessToken(
        long userId,
        string email,
        string fullName,
        string securityStamp,
        IReadOnlyCollection<string> roles,
        bool passwordChangeRequired);

    /// <summary>Generates a cryptographically-random refresh token (raw value, returned once).</summary>
    string GenerateRefreshToken();

    /// <summary>SHA-256 hex (lowercase) hash for storing/looking up a refresh token.</summary>
    string HashRefreshToken(string rawToken);
}
