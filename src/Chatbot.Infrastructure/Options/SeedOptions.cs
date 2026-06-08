namespace Chatbot.Infrastructure.Options;

public sealed class SeedOptions
{
    public const string SectionName = "Seed";

    public AdminSeed Admin { get; set; } = new();

    public sealed class AdminSeed
    {
        public string Email { get; set; } = "admin@chatbot.local";
        public string FullName { get; set; } = "System Administrator";

        /// <summary>Initial admin password. Provided via secrets/env; user must change on first login.</summary>
        public string Password { get; set; } = string.Empty;
    }
}
