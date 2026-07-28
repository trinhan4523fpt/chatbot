using Chatbot.Api.Contracts;
using Chatbot.Application.Common.Interfaces;
using Chatbot.Application.Features.Payment;
using Chatbot.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Chatbot.Api.Controllers;

/// <summary>
/// Quản lý gói token (Admin) + Học sinh mua gói + VnPay callback.
/// </summary>
[ApiController]
[Route("api/v1/payment")]
public sealed class PaymentController(
    IPaymentDbContext db,
    ICurrentUser currentUser,
    IVnPayService vnPay,
    IEmailService email,
    ILogger<PaymentController> logger) : ControllerBase
{
    // ─────────────────────────────────────────────────────────────────────────
    // ADMIN — Quản lý gói token
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Lấy danh sách tất cả gói token.</summary>
    [HttpGet("packages")]
    [AllowAnonymous] // Học sinh cũng cần xem danh sách gói
    public async Task<IActionResult> ListPackages(
        [FromQuery] bool includeInactive = false, CancellationToken ct = default)
    {
        // Nếu không phải Admin → chỉ trả về gói đang active
        var isAdmin = User.IsInRole("Admin");
        var packages = await ListTokenPackages.ExecuteAsync(
            db, new ListTokenPackages.Query(isAdmin && includeInactive), ct);
        return Ok(packages);
    }

    /// <summary>Tạo gói token mới (Admin only).</summary>
    [HttpPost("packages")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreatePackage(
        [FromBody] CreatePackageRequest req, CancellationToken ct = default)
    {
        if (req.TokenAmount <= 0)
            return BadRequest(new { message = "Số lượng token phải lớn hơn 0." });
        if (req.Price < 0)
            return BadRequest(new { message = "Giá gói không hợp lệ." });

        var result = await CreateTokenPackage.ExecuteAsync(
            db, currentUser,
            new CreateTokenPackage.Command(req.Name, req.Description, req.TokenAmount, req.Price, req.ValidityDays, req.DisplayOrder),
            ct);
        return CreatedAtAction(nameof(ListPackages), new { }, result);
    }

    /// <summary>Cập nhật gói token (Admin only).</summary>
    [HttpPut("packages/{id:long}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdatePackage(long id,
        [FromBody] UpdatePackageRequest req, CancellationToken ct = default)
    {
        try
        {
            await UpdateTokenPackage.ExecuteAsync(
                db, new UpdateTokenPackage.Command(id, req.Name, req.Description, req.TokenAmount, req.Price, req.ValidityDays, req.IsActive, req.DisplayOrder), ct);
            return NoContent();
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    /// <summary>Ngưng bán gói token (Admin only).</summary>
    [HttpDelete("packages/{id:long}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeactivatePackage(long id, CancellationToken ct = default)
    {
        try
        {
            await DeactivateTokenPackage.ExecuteAsync(db, id, ct);
            return NoContent();
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // STUDENT — Tạo đơn & ví token
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Học sinh tạo đơn mua gói token → nhận URL chuyển sang VnPay.</summary>
    [HttpPost("orders")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> CreateOrder(
        [FromBody] CreateOrderRequest req, CancellationToken ct = default)
    {
        try
        {
            var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            var returnUrl = req.ReturnUrl ?? $"{Request.Scheme}://{Request.Host}/payment/result";

            var result = await CreateTokenOrder.ExecuteAsync(
                db, currentUser, vnPay,
                new CreateTokenOrder.Command(req.PackageId, returnUrl, clientIp),
                ct);

            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    /// <summary>Học sinh xem lịch sử các đơn hàng mua gói token của chính mình.</summary>
    [HttpGet("orders/me")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> GetMyOrders(
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        OrderStatus? parsedStatus = null;
        if (!string.IsNullOrEmpty(status) && Enum.TryParse<OrderStatus>(status, true, out var s))
            parsedStatus = s;

        var result = await GetMyTokenOrders.ExecuteAsync(
            db, new GetMyTokenOrders.Query(currentUser.UserId!.Value, parsedStatus, page, pageSize), ct);
        return Ok(result);
    }

    /// <summary>
    /// VnPay Return URL — học sinh được redirect về đây sau khi thanh toán.
    /// Trả về JSON để frontend SPA xử lý.
    /// </summary>
    [HttpGet("vnpay/return")]
    [AllowAnonymous]
    public async Task<IActionResult> VnPayReturn(CancellationToken ct = default)
    {
        var queryParams = Request.Query.ToDictionary(kv => kv.Key, kv => kv.Value.ToString());
        var result = await ProcessVnPayCallback.ExecuteAsync(
            db, vnPay, email, logger, new ProcessVnPayCallback.Command(queryParams), ct);

        if (result.Success)
        {
            logger.LogInformation("VnPay return: Đơn hàng #{OrderId} thanh toán thành công, cộng {Tokens} token.",
                result.OrderId, result.TokensAdded);
        }
        else
        {
            logger.LogWarning("VnPay return: Thất bại — {Message}", result.Message);
        }

        return Ok(result);
    }

    /// <summary>
    /// VnPay IPN (Instant Payment Notification) — VnPay server gọi về.
    /// Phải trả về {"RspCode":"00","Message":"Confirm Success"} khi thành công.
    /// </summary>
    [HttpPost("vnpay/ipn")]
    [AllowAnonymous]
    public async Task<IActionResult> VnPayIpn(CancellationToken ct = default)
    {
        var queryParams = Request.Query.ToDictionary(kv => kv.Key, kv => kv.Value.ToString());
        var result = await ProcessVnPayCallback.ExecuteAsync(
            db, vnPay, email, logger, new ProcessVnPayCallback.Command(queryParams), ct);

        if (!result.Success)
        {
            logger.LogWarning("VnPay IPN: Thất bại — {Message}", result.Message);
            // VnPay yêu cầu 97 cho sai chữ ký, 02 cho đơn không tồn tại
            return Ok(new { RspCode = "02", Message = result.Message });
        }

        return Ok(new { RspCode = "00", Message = "Confirm Success" });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // WALLET — Ví token của học sinh
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Xem ví token của chính mình.</summary>
    [HttpGet("wallet/me")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> GetMyWalletInfo(CancellationToken ct = default)
    {
        var wallet = await GetMyWallet.ExecuteAsync(db, currentUser, ct);
        if (wallet is null)
            return Ok(new { message = "Bạn chưa có ví token. Vui lòng mua gói để bắt đầu.", availableTokens = 0 });
        return Ok(wallet);
    }

    /// <summary>Lịch sử giao dịch token của mình.</summary>
    [HttpGet("wallet/me/history")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> GetMyTokenHistory(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await GetTokenHistory.ExecuteAsync(
            db, new GetTokenHistory.Query(currentUser.UserId!.Value, page, pageSize), ct);
        return Ok(result);
    }

    /// <summary>Admin xem ví của học sinh bất kỳ.</summary>
    [HttpGet("wallet/user/{userId:long}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetUserWallet(long userId, CancellationToken ct = default)
    {
        var history = await GetTokenHistory.ExecuteAsync(
            db, new GetTokenHistory.Query(userId, 1, 50), ct);
        return Ok(history);
    }

    /// <summary>Admin điều chỉnh token thủ công cho học sinh.</summary>
    [HttpPost("wallet/admin-adjust")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AdminAdjust(
        [FromBody] AdminAdjustRequest req, CancellationToken ct = default)
    {
        if (req.Delta == 0)
            return BadRequest(new { message = "Delta không được bằng 0." });
        if (string.IsNullOrWhiteSpace(req.Reason))
            return BadRequest(new { message = "Cần ghi lý do điều chỉnh." });

        await AdminAdjustTokens.ExecuteAsync(
            db, new AdminAdjustTokens.Command(req.UserId, req.Delta, req.Reason), ct);

        return Ok(new { message = "Điều chỉnh token thành công." });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ADMIN — Danh sách đơn hàng
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Danh sách đơn hàng có filter + phân trang (Admin).</summary>
    [HttpGet("orders")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ListOrders(
        [FromQuery] string? status = null,
        [FromQuery] long? userId = null,
        [FromQuery] long? packageId = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        OrderStatus? parsedStatus = null;
        if (!string.IsNullOrEmpty(status) && Enum.TryParse<OrderStatus>(status, true, out var s))
            parsedStatus = s;

        var result = await Chatbot.Application.Features.Payment.ListOrders.ExecuteAsync(
            db, new Chatbot.Application.Features.Payment.ListOrders.Query(parsedStatus, userId, packageId, from, to, null, page, pageSize), ct);
        return Ok(result);
    }
}
