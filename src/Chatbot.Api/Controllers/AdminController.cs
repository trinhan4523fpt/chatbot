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
    IMemoryCache cache,
    IPasswordHasher passwordHasher,
    ILogger<AdminController> logger) : ControllerBase
{
    /// <summary>
    /// Kích hoạt tài khoản và đặt mật khẩu mới — không yêu cầu đăng nhập.
    /// Người dùng nhận link trong email, frontend gọi endpoint này với:
    ///   email    : địa chỉ email tài khoản
    ///   code     : mã OTP 6 chữ số trong link kích hoạt
    ///   newPassword : mật khẩu mới họ muốn đặt
    /// </summary>
    [AllowAnonymous]
    [HttpPost("users/confirm-and-setup-password")]
    public async Task<IActionResult> ConfirmAndSetupPassword(
        [FromBody] ConfirmAndSetupPasswordRequest request, CancellationToken ct)
    {
        // Validate đầu vào cơ bản
        if (string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Code) ||
            string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return BadRequest(new { message = "Email, mã kích hoạt và mật khẩu mới không được để trống." });
        }

        if (request.NewPassword.Length < 8)
        {
            return BadRequest(new { message = "Mật khẩu mới phải có ít nhất 8 ký tự." });
        }

        // Kiểm tra mã kích hoạt trong cache
        var normalizedEmail = request.Email.ToUpperInvariant();
        var cacheKey = $"email_confirm:{normalizedEmail}";

        if (!cache.TryGetValue<string>(cacheKey, out var storedCode) || storedCode != request.Code)
        {
            return BadRequest(new { message = "Mã kích hoạt không hợp lệ hoặc đã hết hạn (24 giờ). Vui lòng liên hệ quản trị viên để được cấp lại." });
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
            return BadRequest(new { message = "Tài khoản này đã được kích hoạt trước đó. Vui lòng đăng nhập bình thường." });
        }

        // Kích hoạt tài khoản + cập nhật mật khẩu mới do user tự chọn
        user.EmailConfirmed = true;
        user.PasswordHash = passwordHasher.Hash(request.NewPassword);
        user.SecurityStamp = Guid.NewGuid().ToString("N"); // invalidate cũ nếu có token nào đó

        await db.SaveChangesAsync(ct);

        // Xóa mã OTP khỏi cache sau khi dùng
        cache.Remove(cacheKey);

        logger.LogInformation("User {Email} kích hoạt tài khoản thành công.", user.Email);

        return Ok(new { message = "Tài khoản đã được kích hoạt thành công. Bạn có thể đăng nhập ngay với mật khẩu vừa đặt." });
    }
}
