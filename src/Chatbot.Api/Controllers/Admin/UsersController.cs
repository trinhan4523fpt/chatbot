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

        // Sinh mã kích hoạt OTP 6 chữ số (crypto-safe)
        var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        var cacheKey = $"email_confirm:{request.Email.ToUpperInvariant()}";
        cache.Set(cacheKey, code, TimeSpan.FromHours(24));

        // Tạo link kích hoạt trỏ về frontend
        var opts = emailOptions.Value;
        var activationLink =
            $"{opts.ClientUrl.TrimEnd('/')}/setup-account" +
            $"?email={Uri.EscapeDataString(request.Email)}&code={code}";

        // Gửi email kích hoạt
        var subject = "Kích hoạt tài khoản - Chatbot Learning System";
        var body = $"""
            <div style="font-family:'Segoe UI',Tahoma,Geneva,Verdana,sans-serif;max-width:600px;margin:0 auto;padding:20px;border:1px solid #e2e8f0;border-radius:8px;background:#ffffff;color:#1a202c;">
                <div style="text-align:center;margin-bottom:24px;padding-bottom:20px;border-bottom:2px solid #edf2f7;">
                    <h2 style="color:#2b6cb0;margin:0;font-size:24px;">Chatbot Learning System</h2>
                    <p style="color:#718096;margin:5px 0 0;font-size:14px;">Kích hoạt tài khoản của bạn</p>
                </div>

                <div style="line-height:1.7;font-size:15px;">
                    <p>Xin chào <strong>{request.FullName}</strong>,</p>
                    <p>Tài khoản của bạn đã được tạo thành công trên hệ thống. Dưới đây là thông tin đăng nhập <strong>tạm thời</strong>:</p>

                    <div style="background:#f7fafc;border-left:4px solid #2b6cb0;padding:16px;margin:20px 0;border-radius:4px;">
                        <p style="margin:0 0 8px;"><strong>Email đăng nhập:</strong>
                            <span style="font-family:monospace;font-size:15px;color:#2d3748;">{request.Email}</span></p>
                        <p style="margin:0;"><strong>Mật khẩu tạm thời:</strong>
                            <span style="font-family:monospace;font-size:15px;color:#e53e3e;font-weight:bold;background:#fff;padding:2px 6px;border:1px dashed #cbd5e0;border-radius:3px;">{result.TempPassword}</span></p>
                    </div>

                    <p style="color:#e53e3e;font-weight:bold;">⚠️ Quan trọng:</p>
                    <ul style="padding-left:20px;margin-top:5px;">
                        <li>Mật khẩu trên chỉ là tạm thời, dùng để xác nhận danh tính.</li>
                        <li>Nhấn vào nút bên dưới để <strong>kích hoạt tài khoản và đặt mật khẩu mới</strong> theo ý bạn.</li>
                        <li>Link này có hiệu lực trong <strong>24 giờ</strong>.</li>
                    </ul>

                    <div style="text-align:center;margin:32px 0;">
                        <a href="{activationLink}"
                           style="display:inline-block;background:#2b6cb0;color:#ffffff;text-decoration:none;padding:14px 32px;border-radius:6px;font-size:16px;font-weight:bold;">
                            ✅ Kích hoạt tài khoản &amp; đặt mật khẩu mới
                        </a>
                    </div>

                    <p style="font-size:13px;color:#718096;">
                        Nếu nút trên không hoạt động, hãy copy link sau vào trình duyệt:<br/>
                        <a href="{activationLink}" style="color:#2b6cb0;word-break:break-all;">{activationLink}</a>
                    </p>
                </div>

                <div style="margin-top:30px;padding-top:20px;border-top:1px solid #edf2f7;text-align:center;font-size:12px;color:#a0aec0;">
                    <p>Đây là email tự động. Vui lòng không phản hồi lại email này.</p>
                    <p>&copy; {DateTime.UtcNow.Year} Chatbot Learning System. All rights reserved.</p>
                </div>
            </div>
            """;

        await emailService.SendEmailAsync(request.Email, subject, body);
        logger.LogInformation("Đã gửi email kích hoạt tài khoản tới {Email}.", request.Email);

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
