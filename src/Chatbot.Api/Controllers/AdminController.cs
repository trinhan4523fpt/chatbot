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
    IPasswordHasher passwordHasher,
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

        var user = await db.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, ct);

        if (user is null)
        {
            return NotFound(new { message = "Không tìm thấy người dùng tương ứng với email này." });
        }

        var isInstructor = user.UserRoles.Any(ur => ur.Role.Name == RoleDefinitions.Instructor);
        user.EmailConfirmed = true;

        string? tempPassword = null;
        if (isInstructor)
        {
            tempPassword = GenerateTemporaryPassword();
            user.PasswordHash = passwordHasher.Hash(tempPassword);
            user.MustChangePassword = true;

            var subject = "Thông tin tài khoản giảng viên - Chatbot Learning System";
            var body = $"""
                <div style="font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #e2e8f0; border-radius: 8px; background-color: #ffffff; color: #1a202c;">
                    <div style="text-align: center; margin-bottom: 24px; padding-bottom: 20px; border-bottom: 2px solid #edf2f7;">
                        <h2 style="color: #2b6cb0; margin: 0; font-size: 24px;">Chatbot Learning System</h2>
                        <p style="color: #718096; margin: 5px 0 0 0; font-size: 14px;">Thông tin tài khoản giảng viên</p>
                    </div>
                    
                    <div style="line-height: 1.6; font-size: 16px;">
                        <p>Xin chào <strong>{user.FullName}</strong>,</p>
                        <p>Gmail của bạn đã được xác nhận thành công. Dưới đây là thông tin tài khoản đăng nhập của bạn:</p>
                        
                        <div style="background-color: #f7fafc; border-left: 4px solid #2b6cb0; padding: 16px; margin: 20px 0; border-radius: 4px;">
                            <p style="margin: 0 0 8px 0;"><strong>Tài khoản (Gmail):</strong> <span style="font-family: monospace; font-size: 15px; color: #2d3748;">{user.Email}</span></p>
                            <p style="margin: 0;"><strong>Mật khẩu tạm thời:</strong> <span style="font-family: monospace; font-size: 15px; color: #e53e3e; font-weight: bold; background: #fff; padding: 2px 6px; border: 1px dashed #cbd5e0; border-radius: 3px;">{tempPassword}</span></p>
                        </div>
                        
                        <p style="color: #e53e3e; font-weight: bold;">Lưu ý quan trọng:</p>
                        <ul style="padding-left: 20px; margin-top: 5px;">
                            <li>Mật khẩu trên chỉ là mật khẩu tạm thời.</li>
                            <li>Bạn sẽ được yêu cầu <strong>thay đổi mật khẩu</strong> ngay trong lần đăng nhập đầu tiên để bảo mật tài khoản.</li>
                        </ul>
                    </div>
                    
                    <div style="margin-top: 30px; padding-top: 20px; border-top: 1px solid #edf2f7; text-align: center; font-size: 12px; color: #a0aec0; line-height: 1.5;">
                        <p>Đây là email tự động từ hệ thống. Vui lòng không phản hồi lại email này.</p>
                        <p>&copy; {DateTime.UtcNow.Year} Chatbot Learning System. All rights reserved.</p>
                    </div>
                </div>
                """;

            await emailService.SendEmailAsync(user.Email, subject, body);
        }

        await db.SaveChangesAsync(ct);

        // Remove from cache
        cache.Remove(cacheKey);

        logger.LogInformation("User {Email} successfully confirmed their email.", user.Email);

        return Ok(new { message = "Xác nhận Gmail thành công." });
    }

    private static string GenerateTemporaryPassword()
    {
        const string lower = "abcdefghijklmnopqrstuvwxyz";
        const string upper = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        const string digits = "0123456789";
        
        var random = new Random();
        var chars = new char[12];
        
        chars[0] = lower[random.Next(lower.Length)];
        chars[1] = upper[random.Next(upper.Length)];
        chars[2] = digits[random.Next(digits.Length)];
        
        const string all = lower + upper + digits;
        for (int i = 3; i < chars.Length; i++)
        {
            chars[i] = all[random.Next(all.Length)];
        }
        
        for (int i = chars.Length - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            var temp = chars[i];
            chars[i] = chars[j];
            chars[j] = temp;
        }
        
        return new string(chars);
    }
}
