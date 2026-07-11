namespace Chatbot.Infrastructure.Options;

public sealed class EmailOptions
{
    public const string SectionName = "EmailSettings";

    public string SmtpServer { get; set; } = string.Empty;
    public int Port { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public string SenderEmail { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    /// <summary>URL của frontend (dùng khi cần redirect về SPA).</summary>
    public string ClientUrl { get; set; } = "http://localhost:5173";

    /// <summary>URL gốc của backend API, dùng để tạo link xác nhận email trỏ thẳng vào API.</summary>
    public string ApiUrl { get; set; } = "http://localhost:5024";
}
