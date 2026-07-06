namespace Chatbot.Infrastructure.Options;

public sealed class EmailOptions
{
    public const string SectionName = "EmailSettings";

    public string SmtpServer { get; set; } = string.Empty;
    public int Port { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public string SenderEmail { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    /// <summary>URL của frontend, dùng để tạo link kích hoạt tài khoản trong email.</summary>
    public string ClientUrl { get; set; } = "http://localhost:5173";
}
