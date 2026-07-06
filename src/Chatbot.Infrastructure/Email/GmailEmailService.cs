using Chatbot.Application.Common.Interfaces;
using Chatbot.Infrastructure.Options;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using System.Threading.Tasks;

namespace Chatbot.Infrastructure.Email;

public class GmailEmailService(IOptions<EmailOptions> emailOptions) : IEmailService
{
    private readonly EmailOptions _emailOptions = emailOptions.Value;

    public async Task SendEmailAsync(string toEmail, string subject, string htmlBody)
    {
        var email = new MimeMessage();

        email.From.Add(new MailboxAddress(_emailOptions.SenderName, _emailOptions.SenderEmail));
        email.To.Add(MailboxAddress.Parse(toEmail));
        email.Subject = subject;

        var builder = new BodyBuilder
        {
            HtmlBody = htmlBody
        };

        email.Body = builder.ToMessageBody();

        using var smtp = new SmtpClient();

        await smtp.ConnectAsync(
            _emailOptions.SmtpServer,
            _emailOptions.Port,
            SecureSocketOptions.StartTls);

        await smtp.AuthenticateAsync(
            _emailOptions.SenderEmail,
            _emailOptions.Password);

        await smtp.SendAsync(email);
        await smtp.DisconnectAsync(true);
    }
}
