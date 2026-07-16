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
    /// Xác nhận email bằng 1 click — dùng cho link trong email gửi tới người dùng.
    /// Nhận email và code qua query string, trả về trang HTML (không yêu cầu đăng nhập).
    /// Sau khi xác nhận thành công, hệ thống tự gửi email thứ 2 chứa mật khẩu tạm thời.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("users/confirm-email")]
    public async Task<ContentResult> ConfirmEmailByLink(
        [FromQuery] string email, [FromQuery] string code, CancellationToken ct)
    {
        static ContentResult Html(string title, string color, string icon, string heading, string body) =>
            new()
            {
                ContentType = "text/html; charset=utf-8",
                StatusCode = 200,
                Content = $$"""
                    <!DOCTYPE html>
                    <html lang="vi">
                    <head>
                        <meta charset="UTF-8"/>
                        <meta name="viewport" content="width=device-width,initial-scale=1"/>
                        <title>{{title}}</title>
                        <style>
                            * { box-sizing: border-box; margin: 0; padding: 0; }
                            body { font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; background: #f0f4f8; display: flex; align-items: center; justify-content: center; min-height: 100vh; padding: 20px; }
                            .card { background: #fff; border-radius: 16px; box-shadow: 0 8px 32px rgba(0,0,0,.12); padding: 48px 40px; max-width: 480px; width: 100%; text-align: center; }
                            .icon { font-size: 64px; margin-bottom: 20px; }
                            h1 { color: {{color}}; font-size: 24px; margin-bottom: 12px; }
                            p { color: #4a5568; line-height: 1.7; font-size: 15px; }
                            .brand { margin-top: 36px; padding-top: 20px; border-top: 1px solid #edf2f7; font-size: 12px; color: #a0aec0; }
                        </style>
                    </head>
                    <body>
                        <div class="card">
                            <div class="icon">{{icon}}</div>
                            <h1>{{heading}}</h1>
                            <p>{{body}}</p>
                            <div class="brand">© {{DateTime.UtcNow.Year}} Chatbot Learning System</div>
                        </div>
                    </body>
                    </html>
                    """
            };


        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(code))
            return Html("Lỗi xác nhận", "#e53e3e", "⚠️",
                "Đường dẫn không hợp lệ",
                "Link xác nhận bị thiếu thông tin. Vui lòng liên hệ quản trị viên để được cấp lại.");

        var normalizedEmail = email.ToUpperInvariant();
        var cacheKey = $"email_confirm:{normalizedEmail}";

        if (!cache.TryGetValue<string>(cacheKey, out var storedCode) || storedCode != code)
            return Html("Lỗi xác nhận", "#e53e3e", "⏰",
                "Link đã hết hạn hoặc không hợp lệ",
                "Mã xác nhận không còn hiệu lực (hết hạn sau 24 giờ). Vui lòng liên hệ quản trị viên để được gửi lại email xác nhận.");

        var user = await db.Users.FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, ct);
        if (user is null)
            return Html("Không tìm thấy", "#e53e3e", "❓",
                "Không tìm thấy tài khoản",
                "Không có tài khoản nào tương ứng với email này.");

        if (user.EmailConfirmed)
            return Html("Đã xác nhận", "#2b6cb0", "ℹ️",
                "Email đã được xác nhận trước đó",
                "Tài khoản của bạn đã được kích hoạt. Vui lòng đăng nhập bình thường.");

        // Xác nhận email
        user.EmailConfirmed = true;
        await db.SaveChangesAsync(ct);
        cache.Remove(cacheKey);

        logger.LogInformation("User {Email} xác nhận email thành công qua link.", user.Email);

        // Lấy mật khẩu tạm từ cache
        var pwCacheKey = $"temp_password:{normalizedEmail}";
        if (!cache.TryGetValue<string>(pwCacheKey, out var tempPassword) || string.IsNullOrEmpty(tempPassword))
        {
            logger.LogWarning("Không tìm thấy mật khẩu tạm cho {Email} sau khi xác nhận.", user.Email);
            return Html("Xác nhận thành công", "#38a169", "✅",
                "Email đã được xác nhận!",
                "Tuy nhiên thông tin đăng nhập đã hết hạn trong hệ thống. Vui lòng liên hệ quản trị viên để được cấp lại mật khẩu.");
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

                    <div style="text-align:center;margin:28px 0 10px;">
                        <a href="https://chatbot-api.viberp.vn/scalar/v1#tag/admin"
                           style="display:inline-block;background:#2b6cb0;color:#ffffff;text-decoration:none;padding:12px 28px;border-radius:6px;font-size:15px;font-weight:bold;">
                            👉 Đi tới Trang quản trị (API Docs)
                        </a>
                    </div>
                </div>

                <div style="margin-top:30px;padding-top:20px;border-top:1px solid #edf2f7;text-align:center;font-size:12px;color:#a0aec0;">
                    <p>Đây là email tự động. Vui lòng không phản hồi lại email này.</p>
                    <p>&copy; {DateTime.UtcNow.Year} Chatbot Learning System. All rights reserved.</p>
                </div>
            </div>
            """;

        await emailService.SendEmailAsync(user.Email, accountSubject, accountBody);
        cache.Remove(pwCacheKey);

        logger.LogInformation("Đã gửi email thông tin tài khoản tới {Email} sau khi xác nhận.", user.Email);

        return Html("Xác nhận thành công", "#38a169", "🎉",
            "Email đã được xác nhận thành công!",
            $"Xin chào <strong>{user.FullName}</strong>! Thông tin tài khoản và mật khẩu đăng nhập đã được gửi đến hộp thư <strong>{user.Email}</strong>. Vui lòng kiểm tra email.<br/><br/><div style='text-align:center;margin-top:20px;'><a href='https://chatbot-api.viberp.vn/scalar/v1#tag/admin' style='display:inline-block;background:#38a169;color:#ffffff;text-decoration:none;padding:12px 24px;border-radius:6px;font-weight:bold;font-size:15px;'>👉 Đi tới Trang quản trị (API Docs)</a></div>");
    }

    /// <summary>
    /// Xác nhận địa chỉ email qua API (dùng cho frontend SPA).
    /// email : địa chỉ email tài khoản
    /// code  : mã OTP 6 chữ số trong link xác nhận
    /// </summary>
    [AllowAnonymous]
    [HttpPost("users/confirm-email")]
    public async Task<IActionResult> ConfirmEmail(
        [FromBody] ConfirmEmailRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Code))
            return BadRequest(new { message = "Email và mã xác nhận không được để trống." });

        var normalizedEmail = request.Email.ToUpperInvariant();
        var cacheKey = $"email_confirm:{normalizedEmail}";

        if (!cache.TryGetValue<string>(cacheKey, out var storedCode) || storedCode != request.Code)
            return BadRequest(new { message = "Mã xác nhận không hợp lệ hoặc đã hết hạn (24 giờ). Vui lòng liên hệ quản trị viên để được cấp lại." });

        var user = await db.Users.FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, ct);
        if (user is null)
            return NotFound(new { message = "Không tìm thấy tài khoản tương ứng với email này." });

        if (user.EmailConfirmed)
            return BadRequest(new { message = "Email này đã được xác nhận trước đó. Vui lòng đăng nhập bình thường." });

        user.EmailConfirmed = true;
        await db.SaveChangesAsync(ct);
        cache.Remove(cacheKey);

        logger.LogInformation("User {Email} xác nhận email thành công.", user.Email);

        var pwCacheKey = $"temp_password:{normalizedEmail}";
        if (!cache.TryGetValue<string>(pwCacheKey, out var tempPassword) || string.IsNullOrEmpty(tempPassword))
        {
            logger.LogWarning("Không tìm thấy mật khẩu tạm thời trong cache cho {Email}.", user.Email);
            return Ok(new { message = "Email đã được xác nhận thành công. Tuy nhiên, thông tin đăng nhập đã hết hạn trong hệ thống, vui lòng liên hệ quản trị viên để được cấp lại mật khẩu." });
        }

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
                    <div style="background:#f7fafc;border-left:4px solid #38a169;padding:16px;margin:20px 0;border-radius:4px;">
                        <p style="margin:0 0 8px;"><strong>Email đăng nhập:</strong> <span style="font-family:monospace;">{user.Email}</span></p>
                        <p style="margin:0;"><strong>Mật khẩu tạm thời:</strong> <span style="font-family:monospace;color:#e53e3e;font-weight:bold;">{tempPassword}</span></p>
                    </div>

                    <div style="text-align:center;margin:28px 0 10px;">
                        <a href="https://chatbot-api.viberp.vn/scalar/v1#tag/admin"
                           style="display:inline-block;background:#2b6cb0;color:#ffffff;text-decoration:none;padding:12px 28px;border-radius:6px;font-size:15px;font-weight:bold;">
                            👉 Đi tới Trang quản trị (API Docs)
                        </a>
                    </div>
                </div>
                <div style="margin-top:30px;padding-top:20px;border-top:1px solid #edf2f7;text-align:center;font-size:12px;color:#a0aec0;">
                    <p>&copy; {DateTime.UtcNow.Year} Chatbot Learning System. All rights reserved.</p>
                </div>
            </div>
            """;

        await emailService.SendEmailAsync(user.Email, accountSubject, accountBody);
        cache.Remove(pwCacheKey);

        logger.LogInformation("Đã gửi email thông tin tài khoản tới {Email}.", user.Email);

        return Ok(new { message = "Email đã được xác nhận thành công. Thông tin tài khoản và mật khẩu đã được gửi đến email của bạn." });
    }
}
