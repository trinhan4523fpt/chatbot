using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Chatbot.Application.Common.Interfaces;
using Chatbot.Infrastructure.Options;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Chatbot.Infrastructure.Identity;

public sealed class JwtTokenService(IOptions<JwtOptions> options, IClock clock) : IJwtTokenService
{
    public const string SecurityStampClaim = "security_stamp";
    public const string RoleClaim = "role";

    private readonly JwtOptions _options = options.Value;

    public AccessToken CreateAccessToken(
        long userId, string email, string fullName, string securityStamp,
        IReadOnlyCollection<string> roles)
    {
        var now = clock.UtcNow;
        var expires = now.AddMinutes(_options.AccessTokenMinutes);
        var jwtId = Guid.NewGuid().ToString("N");

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            new(JwtRegisteredClaimNames.Name, fullName),
            new(JwtRegisteredClaimNames.Jti, jwtId),
            new(SecurityStampClaim, securityStamp),
        };
        foreach (var role in roles)
        {
            claims.Add(new Claim(RoleClaim, role));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = expires,
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256),
        };

        var token = new JsonWebTokenHandler().CreateToken(descriptor);
        return new AccessToken(token, expires, jwtId);
    }

    public string GenerateRefreshToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    public string HashRefreshToken(string rawToken) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
}
