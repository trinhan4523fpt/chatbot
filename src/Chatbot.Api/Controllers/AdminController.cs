using Chatbot.Api.Authorization;
using Chatbot.Api.Contracts;
using Chatbot.Application.Common.Authorization;
using Chatbot.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Chatbot.Api.Controllers;

[ApiController]
[Route("api/v1/admin")]
public sealed class AdminController(
    IAppDbContext db,
    IEmailService emailService,
    IMemoryCache cache,
    ILogger<AdminController> logger) : ControllerBase
{
    [Authorize]
    [HasPermission(Permissions.Users.Update)]
    [HttpPost("users/{userId:long}/send-confirmation-email")]
    public async Task<IActionResult> SendConfirmationEmail(long userId, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null)
        {
            return NotFound(new { message = "Không tìm thấy người dùng." });
        }

        // Generate 6-digit random code
        var random = new Random();
        var code = random.Next(100000, 999999).ToString();

        // Store in cache for 15 minutes
        var cacheKey = $"email_confirm:{user.Email.ToUpperInvariant()}";
        cache.Set(cacheKey, code, TimeSpan.FromMinutes(15));

        // Send email
        var subject = "Xác nhận địa chỉ Gmail - Chatbot Learning System";
        var body = $"""
            <h3>Xin chào {user.FullName},</h3>
            <p>Mã xác nhận Gmail của bạn là: <strong>{code}</strong></p>
            <p>Mã này có hiệu lực trong vòng 15 phút.</p>
            """;

        await emailService.SendEmailAsync(user.Email, subject, body);

        logger.LogInformation("Sent email confirmation code to {Email}", user.Email);

        return Ok(new { message = "Đã gửi mã xác nhận đến email của người dùng." });
    }

    [AllowAnonymous]
    [HttpPost("users/confirm-email")]
    public async Task<IActionResult> ConfirmEmail([FromBody] ConfirmEmailRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Code))
        {
            return BadRequest(new { message = "Email và mã xác nhận không được để trống." });
        }

        var normalizedEmail = request.Email.ToUpperInvariant();
        var cacheKey = $"email_confirm:{normalizedEmail}";

        if (!cache.TryGetValue<string>(cacheKey, out var storedCode) || storedCode != request.Code)
        {
            return BadRequest(new { message = "Mã xác nhận không hợp lệ hoặc đã hết hạn." });
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, ct);
        if (user is null)
        {
            return NotFound(new { message = "Không tìm thấy người dùng tương ứng với email này." });
        }

        user.EmailConfirmed = true;
        await db.SaveChangesAsync(ct);

        // Remove from cache
        cache.Remove(cacheKey);

        logger.LogInformation("User {Email} successfully confirmed their email.", user.Email);

        return Ok(new { message = "Xác nhận Gmail thành công." });
    }
}
