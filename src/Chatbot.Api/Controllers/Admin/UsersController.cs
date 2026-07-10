using Chatbot.Api.Authorization;
using Chatbot.Api.Contracts;
using Chatbot.Application.Common.Authorization;
using Chatbot.Application.Common.Interfaces;
using Chatbot.Application.Common.Models;
using Chatbot.Application.Features.Admin.Users;
using Chatbot.Infrastructure.Options;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;

namespace Chatbot.Api.Controllers.Admin;

[ApiController]
[Route("api/v1/admin/users")]
public sealed class UsersController(
    ISender mediator,
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
    /// Hệ thống tự sinh mật khẩu tạm thời, lưu mã kích hoạt OTP vào cache (24h),
    /// rồi gửi email chứa mật khẩu tạm + link kích hoạt để người dùng đặt mật khẩu mới.
    /// </summary>
    [HasPermission(Permissions.Users.Create)]
    [HttpPost]
    public async Task<ActionResult<object>> Create(CreateUserRequest request, CancellationToken ct)
    {
        // Tạo user — handler luôn sinh mật khẩu ngẫu nhiên và trả về
        var result = await mediator.Send(
            new CreateUserCommand(request.Email, request.FullName, request.Roles ?? []), ct);

        // Sinh mã xác nhận OTP 6 chữ số (crypto-safe)
        var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        var normalizedEmail = request.Email.ToUpperInvariant();
        var cacheKey = $"email_confirm:{normalizedEmail}";
        cache.Set(cacheKey, code, TimeSpan.FromHours(24));

        // Lưu mật khẩu tạm thời vào cache — sẽ gửi sau khi user xác nhận email
        var pwCacheKey = $"temp_password:{normalizedEmail}";
        cache.Set(pwCacheKey, result.TempPassword, TimeSpan.FromHours(24));

        // Tạo link xác nhận email trỏ về frontend
        var opts = emailOptions.Value;
        var confirmLink =
            $"{opts.ClientUrl.TrimEnd('/')}/confirm-email" +
            $"?email={Uri.EscapeDataString(request.Email)}&code={code}";

        // Gửi email xác nhận địa chỉ email (chưa có tài khoản/mật khẩu)
        var subject = "Xác nhận email - Chatbot Learning System";
        var body = $"""
            <div style="font-family:'Segoe UI',Tahoma,Geneva,Verdana,sans-serif;max-width:600px;margin:0 auto;padding:20px;border:1px solid #e2e8f0;border-radius:8px;background:#ffffff;color:#1a202c;">
                <div style="text-align:center;margin-bottom:24px;padding-bottom:20px;border-bottom:2px solid #edf2f7;">
                    <h2 style="color:#2b6cb0;margin:0;font-size:24px;">Chatbot Learning System</h2>
                    <p style="color:#718096;margin:5px 0 0;font-size:14px;">Xác nhận địa chỉ email của bạn</p>
                </div>

                <div style="line-height:1.7;font-size:15px;">
                    <p>Xin chào <strong>{request.FullName}</strong>,</p>
                    <p>Quản trị viên đã tạo tài khoản cho bạn trên hệ thống <strong>Chatbot Learning System</strong>.</p>
                    <p>Để hoàn tất đăng ký, vui lòng xác nhận địa chỉ email của bạn bằng cách nhấn vào nút bên dưới:</p>

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

        await emailService.SendEmailAsync(request.Email, subject, body);
        logger.LogInformation("Đã gửi email xác nhận địa chỉ email tới {Email}.", request.Email);

        return CreatedAtAction(nameof(List), new { id = result.Id }, new { id = result.Id });
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
}
