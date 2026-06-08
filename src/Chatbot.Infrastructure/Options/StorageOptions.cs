namespace Chatbot.Infrastructure.Options;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>Storage root (outside web root). Relative paths resolve against the current directory.</summary>
    public string Root { get; set; } = "storage";

    public long MaxUploadBytes { get; set; } = 50L * 1024 * 1024;
}
