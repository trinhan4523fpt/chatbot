using Chatbot.Api.Contracts;
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
    IMemoryCache cache,
    IEmailService emailService,
    ILogger<AdminController> logger) : ControllerBase
{
    /// <summary>
    /// Xác nhận địa chỉ email — không yêu cầu đăng nhập.
    /// Người dùng nhận link trong email, frontend gọi endpoint này với:
    ///   email : địa chỉ email tài khoản
    ///   code  : mã OTP 6 chữ số trong link xác nhận
    /// Sau khi xác nhận thành công, hệ thống tự động gửi email thứ 2
    /// chứa thông tin tài khoản và mật khẩu tạm thời.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("users/confirm-email")]
    public async Task<IActionResult> ConfirmEmail(
        [FromBody] ConfirmEmailRequest request, CancellationToken ct)
    {
        // Validate đầu vào cơ bản
        if (string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Code))
        {
            return BadRequest(new { message = "Email và mã xác nhận không được để trống." });
        }

        // Kiểm tra mã xác nhận trong cache
        var normalizedEmail = request.Email.ToUpperInvariant();
        var cacheKey = $"email_confirm:{normalizedEmail}";

        if (!cache.TryGetValue<string>(cacheKey, out var storedCode) || storedCode != request.Code)
        {
            return BadRequest(new { message = "Mã xác nhận không hợp lệ hoặc đã hết hạn (24 giờ). Vui lòng liên hệ quản trị viên để được cấp lại." });
        }

        // Tìm user theo email
        var user = await db.Users
            .FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, ct);

        if (user is null)
        {
            return NotFound(new { message = "Không tìm thấy tài khoản tương ứng với email này." });
        }

        if (user.EmailConfirmed)
        {
            return BadRequest(new { message = "Email này đã được xác nhận trước đó. Vui lòng đăng nhập bình thường." });
        }

        // Xác nhận email
        user.EmailConfirmed = true;
<<<<<<< HEAD
=======
        user.PasswordHash = passwordHasher.Hash(request.NewPassword);
        user.SecurityStamp = Guid.NewGuid().ToString("N"); // invalidate cũ nếu có token nào đó

>>>>>>> cf89933c55bf605493d9c00fb25a64d12c769dc6
        await db.SaveChangesAsync(ct);

        // Xóa mã OTP xác nhận khỏi cache
        cache.Remove(cacheKey);

        logger.LogInformation("User {Email} xác nhận email thành công.", user.Email);

        // Lấy mật khẩu tạm thời từ cache
        var pwCacheKey = $"temp_password:{normalizedEmail}";
        if (!cache.TryGetValue<string>(pwCacheKey, out var tempPassword) || string.IsNullOrEmpty(tempPassword))
        {
            // Mật khẩu tạm đã hết hạn cache — vẫn xác nhận email thành công nhưng không gửi được mật khẩu
            logger.LogWarning("Không tìm thấy mật khẩu tạm thời trong cache cho {Email}. Xác nhận email vẫn thành công.", user.Email);
            return Ok(new { message = "Email đã được xác nhận thành công. Tuy nhiên, thông tin đăng nhập đã hết hạn trong hệ thống, vui lòng liên hệ quản trị viên để được cấp lại mật khẩu." });
        }

        // Gửi email thứ 2: thông tin tài khoản + mật khẩu tạm thời
        var accountSubject = "Thông tin tài khoản - Chatbot Learning System";
        var accountBody = $"""
            <div style="font-family:'Segoe UI',Tahoma,Geneva,Verdana,sans-serif;max-width:600px;margin:0 auto;padding:20px;border:1px solid #e2e8f0;border-radius:8px;background:#ffffff;color:#1a202c;">
                <div style="text-align:center;margin-bottom:24px;padding-bottom:20px;border-bottom:2px solid #edf2f7;">
                    <h2 style="color:#2b6cb0;margin:0;font-size:24px;">Chatbot Learning System</h2>
                    <p style="color:#718096;margin:5px 0 0;font-size:14px;">Thông tin tài khoản của bạn</p>
                </div>

                <div style="line-height:1.7;font-size:15px;">
                    <p>Xin chào <strong>{user.FullName}</strong>,</p>
                    <p>Email của bạn đã được xác nhận thành công! 🎉</p>
                    <p>Dưới đây là thông tin đăng nhập tài khoản của bạn:</p>

                    <div style="background:#f7fafc;border-left:4px solid #38a169;padding:16px;margin:20px 0;border-radius:4px;">
                        <p style="margin:0 0 8px;"><strong>Email đăng nhập:</strong>
                            <span style="font-family:monospace;font-size:15px;color:#2d3748;">{user.Email}</span></p>
                        <p style="margin:0;"><strong>Mật khẩu tạm thời:</strong>
                            <span style="font-family:monospace;font-size:15px;color:#e53e3e;font-weight:bold;background:#fff;padding:2px 6px;border:1px dashed #cbd5e0;border-radius:3px;">{tempPassword}</span></p>
                    </div>

                    <p style="color:#e53e3e;font-weight:bold;">⚠️ Quan trọng:</p>
                    <ul style="padding-left:20px;margin-top:5px;">
                        <li>Đây là mật khẩu <strong>tạm thời</strong> do hệ thống cấp.</li>
                        <li>Vui lòng đăng nhập và <strong>đổi mật khẩu ngay</strong> để bảo mật tài khoản.</li>
                        <li>Không chia sẻ thông tin này với người khác.</li>
                    </ul>
                </div>

                <div style="margin-top:30px;padding-top:20px;border-top:1px solid #edf2f7;text-align:center;font-size:12px;color:#a0aec0;">
                    <p>Đây là email tự động. Vui lòng không phản hồi lại email này.</p>
                    <p>&copy; {DateTime.UtcNow.Year} Chatbot Learning System. All rights reserved.</p>
                </div>
            </div>
            """;

        await emailService.SendEmailAsync(user.Email, accountSubject, accountBody);

        // Xóa mật khẩu tạm khỏi cache sau khi đã gửi
        cache.Remove(pwCacheKey);

        logger.LogInformation("Đã gửi email thông tin tài khoản tới {Email} sau khi xác nhận.", user.Email);

        return Ok(new { message = "Email đã được xác nhận thành công. Thông tin tài khoản và mật khẩu đã được gửi đến email của bạn." });
    }
}
