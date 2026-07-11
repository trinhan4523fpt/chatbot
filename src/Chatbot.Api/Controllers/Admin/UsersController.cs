using Chatbot.Api.Authorization;
using Chatbot.Api.Contracts;
using Chatbot.Application.Common.Authorization;
using Chatbot.Application.Common.Interfaces;
using Chatbot.Application.Common.Models;
using Chatbot.Application.Features.Admin.Users;
using Chatbot.Infrastructure.Options;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;

namespace Chatbot.Api.Controllers.Admin;

[ApiController]
[Route("api/v1/admin/users")]
public sealed class UsersController(
    ISender mediator,
    IAppDbContext db,
    IEmailService emailService,
    IMemoryCache cache,
    IOptions<EmailOptions> emailOptions,
    ILogger<UsersController> logger) : ControllerBase
{
    [HasPermission(Permissions.Users.Read)]
    [HttpGet]
    public async Task<ActionResult<PagedResult<AdminUserDto>>> List(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null,
        CancellationToken ct = default) =>
        Ok(await mediator.Send(new ListUsersQuery(page, pageSize, search), ct));

    /// <summary>
    /// Admin tạo tài khoản cho giảng viên / sinh viên.
    /// Hệ thống tự sinh mật khẩu tạm thời, lưu OTP vào cache (24h),
    /// rồi gửi email chứa link kích hoạt trỏ thẳng vào backend — người dùng chỉ cần click là xong.
    /// </summary>
    [HasPermission(Permissions.Users.Create)]
    [HttpPost]
    public async Task<ActionResult<object>> Create(CreateUserRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(
            new CreateUserCommand(request.Email, request.FullName, request.Roles ?? []), ct);

        await SendConfirmationEmailAsync(request.Email, request.FullName, result.TempPassword);

        return CreatedAtAction(nameof(List), new { id = result.Id }, new { id = result.Id });
    }

    /// <summary>
    /// Admin gửi lại email xác nhận cho user chưa kích hoạt.
    /// Sinh OTP mới, lưu vào cache và gửi lại link xác nhận 1-click.
    /// </summary>
    [HasPermission(Permissions.Users.Update)]
    [HttpPost("{id:long}/send-confirmation-email")]
    public async Task<IActionResult> SendConfirmationEmail(long id, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user is null)
            return NotFound(new { message = "Không tìm thấy người dùng." });

        if (user.EmailConfirmed)
            return BadRequest(new { message = "Email của người dùng này đã được xác nhận. Không cần gửi lại." });

        // Lấy mật khẩu tạm từ cache; nếu hết hạn thì sinh mới
        var normalizedEmail = user.Email!.ToUpperInvariant();
        var pwCacheKey = $"temp_password:{normalizedEmail}";
        if (!cache.TryGetValue<string>(pwCacheKey, out var tempPassword) || string.IsNullOrEmpty(tempPassword))
        {
            tempPassword = Convert.ToBase64String(RandomNumberGenerator.GetBytes(12))
                               .Replace("+", "!").Replace("/", "@")[..12] + "Aa1!";
        }

        await SendConfirmationEmailAsync(user.Email!, user.FullName, tempPassword);

        logger.LogInformation("Đã gửi lại email xác nhận cho user {Email} (ID={Id}).", user.Email, id);
        return Ok(new { message = $"Email xác nhận đã được gửi lại tới {user.Email}." });
    }

    [HasPermission(Permissions.Users.AssignRole)]
    [HttpPut("{id:long}/roles")]
    public async Task<IActionResult> AssignRoles(long id, AssignRolesRequest request, CancellationToken ct)
    {
        await mediator.Send(new AssignRolesCommand(id, request.Roles), ct);
        return NoContent();
    }

    [HasPermission(Permissions.Users.ResetPassword)]
    [HttpPost("{id:long}/reset-password")]
    public async Task<IActionResult> ResetPassword(long id, ResetPasswordRequest request, CancellationToken ct)
    {
        await mediator.Send(new ResetUserPasswordCommand(id, request.NewPassword), ct);
        return NoContent();
    }

    [HasPermission(Permissions.Users.Update)]
    [HttpPost("{id:long}/active")]
    public async Task<IActionResult> SetActive(long id, SetActiveRequest request, CancellationToken ct)
    {
        await mediator.Send(new SetUserActiveCommand(id, request.IsActive), ct);
        return NoContent();
    }

    // ── Helper ─────────────────────────────────────────────────────────────
    // Sinh OTP, lưu cache, gửi email xác nhận 1-click trỏ vào backend
    private async Task SendConfirmationEmailAsync(string email, string fullName, string tempPassword)
    {
        var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        var normalizedEmail = email.ToUpperInvariant();

        cache.Set($"email_confirm:{normalizedEmail}", code, TimeSpan.FromHours(24));
        cache.Set($"temp_password:{normalizedEmail}", tempPassword, TimeSpan.FromHours(24));

        var opts = emailOptions.Value;
        var confirmLink =
            $"{opts.ApiUrl.TrimEnd('/')}/api/v1/admin/users/confirm-email" +
            $"?email={Uri.EscapeDataString(email)}&code={code}";

        var subject = "Xác nhận email - Chatbot Learning System";
        var body = $"""
            <div style="font-family:'Segoe UI',Tahoma,Geneva,Verdana,sans-serif;max-width:600px;margin:0 auto;padding:20px;border:1px solid #e2e8f0;border-radius:8px;background:#ffffff;color:#1a202c;">
                <div style="text-align:center;margin-bottom:24px;padding-bottom:20px;border-bottom:2px solid #edf2f7;">
                    <h2 style="color:#2b6cb0;margin:0;font-size:24px;">Chatbot Learning System</h2>
                    <p style="color:#718096;margin:5px 0 0;font-size:14px;">Xác nhận địa chỉ email của bạn</p>
                </div>

                <div style="line-height:1.7;font-size:15px;">
                    <p>Xin chào <strong>{fullName}</strong>,</p>
                    <p>Quản trị viên đã tạo tài khoản cho bạn trên hệ thống <strong>Chatbot Learning System</strong>.</p>
                    <p>Để hoàn tất đăng ký, vui lòng nhấn vào nút bên dưới:</p>

                    <div style="text-align:center;margin:32px 0;">
                        <a href="{confirmLink}"
                           style="display:inline-block;background:#2b6cb0;color:#ffffff;text-decoration:none;padding:14px 32px;border-radius:6px;font-size:16px;font-weight:bold;">
                            ✅ Xác nhận email
                        </a>
                    </div>

                    <p style="color:#e53e3e;font-weight:bold;">⚠️ Lưu ý:</p>
                    <ul style="padding-left:20px;margin-top:5px;">
                        <li>Link xác nhận có hiệu lực trong <strong>24 giờ</strong>.</li>
                        <li>Sau khi xác nhận, hệ thống sẽ gửi thông tin tài khoản và mật khẩu đến email này.</li>
                        <li>Nếu bạn không thực hiện yêu cầu này, vui lòng bỏ qua email.</li>
                    </ul>

                    <p style="font-size:13px;color:#718096;">
                        Nếu nút trên không hoạt động, hãy copy link sau vào trình duyệt:<br/>
                        <a href="{confirmLink}" style="color:#2b6cb0;word-break:break-all;">{confirmLink}</a>
                    </p>
                </div>

                <div style="margin-top:30px;padding-top:20px;border-top:1px solid #edf2f7;text-align:center;font-size:12px;color:#a0aec0;">
                    <p>Đây là email tự động. Vui lòng không phản hồi lại email này.</p>
                    <p>&copy; {DateTime.UtcNow.Year} Chatbot Learning System. All rights reserved.</p>
                </div>
            </div>
            """;

        await emailService.SendEmailAsync(email, subject, body);
        logger.LogInformation("Đã gửi email xác nhận tới {Email}.", email);
    }
}
