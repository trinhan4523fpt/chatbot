using System.ComponentModel.DataAnnotations;

namespace Chatbot.Infrastructure.Options;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public string Issuer { get; set; } = "Chatbot";

    [Required]
    public string Audience { get; set; } = "ChatbotClients";

    /// <summary>HMAC signing key; must be >= 32 bytes (256-bit). Set via secrets/env in non-dev.</summary>
    [Required]
    [MinLength(32)]
    public string SigningKey { get; set; } = string.Empty;

    [Range(1, 240)]
    public int AccessTokenMinutes { get; set; } = 15;

    [Range(1, 365)]
    public int RefreshTokenDays { get; set; } = 7;
}
