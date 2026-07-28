using Chatbot.Api.Contracts;
using Chatbot.Application.Common.Interfaces;
using Chatbot.Application.Features.Payment;
using Chatbot.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Chatbot.Api.Controllers;

/// <summary>
/// Báo cáo thống kê doanh thu — Admin only.
///
/// Endpoints:
///   GET  /summary          — KPI tổng quan
///   GET  /monthly          — Doanh thu 12 tháng (biểu đồ)
///   GET  /daily            — Doanh thu theo ngày (biểu đồ chi tiết)
///   GET  /packages         — Thống kê từng gói
///   GET  /orders           — Danh sách đơn hàng (filter + search + phân trang)
///   GET  /orders/{id}/student  — Lịch sử 1 học sinh
///   POST /orders/{id}/refund   — Hoàn tiền đơn
///   GET  /orders/export-csv    — Xuất CSV
///   GET  /token-usage      — Top học sinh theo token
/// </summary>
[ApiController]
[Route("api/v1/reports/revenue")]
[Authorize(Roles = "Admin")]
public sealed class RevenueReportController(
    IPaymentDbContext db,
    IAppDbContext appDb,
    ILogger<RevenueReportController> logger) : ControllerBase
{
    // ─────────────────────────────────────────────────────────────────────────
    // KPI / DASHBOARD
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// KPI tổng quan: tổng doanh thu, tháng này vs tháng trước, tăng trưởng %,
    /// số đơn (paid/pending/failed), số ví, tỉ lệ chuyển đổi,
    /// token phát hành / tiêu thụ, top 5 gói.
    /// </summary>
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(CancellationToken ct = default)
    {
        var result = await GetRevenueSummary.ExecuteAsync(db, appDb, ct);
        return Ok(result);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // BIỂU ĐỒ
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Doanh thu 12 tháng trong năm — cho biểu đồ cột / đường.
    /// Bao gồm: doanh thu, số đơn, số học sinh mua lần đầu mỗi tháng.
    /// </summary>
    [HttpGet("monthly")]
    public async Task<IActionResult> GetMonthly(
        [FromQuery] int? year = null, CancellationToken ct = default)
    {
        var targetYear = year ?? DateTime.UtcNow.Year;
        var result = await GetMonthlyRevenue.ExecuteAsync(
            db, new GetMonthlyRevenue.Query(targetYear), ct);
        return Ok(new { year = targetYear, data = result });
    }

    /// <summary>
    /// Doanh thu theo ngày trong khoảng thời gian — cho biểu đồ chi tiết.
    /// Tối đa 90 ngày. Mặc định: 30 ngày gần nhất.
    /// </summary>
    [HttpGet("daily")]
    public async Task<IActionResult> GetDaily(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to   = null,
        CancellationToken ct = default)
    {
        var toDate   = (to   ?? DateTime.UtcNow).Date;
        var fromDate = (from ?? toDate.AddDays(-29)).Date;

        var result = await GetDailyRevenue.ExecuteAsync(
            db, new GetDailyRevenue.Query(fromDate, toDate), ct);
        return Ok(new { from = fromDate.ToString("yyyy-MM-dd"), to = toDate.ToString("yyyy-MM-dd"), data = result });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GÓI TOKEN
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Thống kê chi tiết từng gói: số đơn, doanh thu, token phát hành, avg doanh thu/ngày.
    /// </summary>
    [HttpGet("packages")]
    public async Task<IActionResult> GetPackages(CancellationToken ct = default)
    {
        var result = await Chatbot.Application.Features.Payment.GetPackageStats.ExecuteAsync(db, ct);
        return Ok(result);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ĐƠN HÀNG
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Danh sách đơn hàng có filter + tìm kiếm + phân trang.
    /// Filter: status (Pending/Paid/Expired/Failed/Refunded), userId, packageId, from, to.
    /// Search: tìm theo OrderRef, email, họ tên.
    /// </summary>
    [HttpGet("orders")]
    public async Task<IActionResult> GetOrders(
        [FromQuery] string?   status    = null,
        [FromQuery] long?     userId    = null,
        [FromQuery] long?     packageId = null,
        [FromQuery] DateTime? from      = null,
        [FromQuery] DateTime? to        = null,
        [FromQuery] string?   search    = null,
        [FromQuery] int       page      = 1,
        [FromQuery] int       pageSize  = 20,
        CancellationToken ct = default)
    {
        OrderStatus? parsedStatus = null;
        if (!string.IsNullOrEmpty(status) && Enum.TryParse<OrderStatus>(status, true, out var s))
            parsedStatus = s;

        var result = await Chatbot.Application.Features.Payment.ListOrders.ExecuteAsync(
            db, new Chatbot.Application.Features.Payment.ListOrders.Query(
                parsedStatus, userId, packageId, from, to, search, page, pageSize), ct);
        return Ok(result);
    }

    /// <summary>
    /// Lịch sử mua gói của 1 học sinh cụ thể:
    /// thông tin ví + tất cả đơn hàng + 50 giao dịch gần nhất.
    /// </summary>
    [HttpGet("students/{userId:long}/history")]
    public async Task<IActionResult> GetStudentHistory(long userId, CancellationToken ct = default)
    {
        var result = await GetStudentPurchaseHistory.ExecuteAsync(
            db, new GetStudentPurchaseHistory.Query(userId), ct);

        if (result is null)
            return NotFound(new { message = $"Học sinh #{userId} chưa có ví token (chưa mua gói nào)." });

        return Ok(result);
    }

    /// <summary>
    /// Hoàn tiền đơn hàng đã thanh toán.
    /// Đánh dấu Status = Refunded + thu hồi token khỏi ví học sinh.
    /// </summary>
    [HttpPost("orders/{orderId:long}/refund")]
    public async Task<IActionResult> RefundOrder(
        long orderId, [FromBody] RefundOrderRequest req, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.Reason))
            return BadRequest(new { message = "Cần ghi lý do hoàn tiền." });

        var result = await Chatbot.Application.Features.Payment.RefundOrder.ExecuteAsync(
            db, new Chatbot.Application.Features.Payment.RefundOrder.Command(orderId, req.Reason), ct);

        if (!result.Success)
            return BadRequest(new { message = result.Message });

        logger.LogInformation("Admin hoàn tiền đơn #{OrderId}: {Reason}", orderId, req.Reason);
        return Ok(new { message = result.Message });
    }

    /// <summary>
    /// Xuất danh sách đơn hàng ra file CSV (UTF-8 BOM).
    /// Excel mở được tiếng Việt không bị lỗi font.
    /// </summary>
    [HttpGet("orders/export-csv")]
    public async Task<IActionResult> ExportCsv(
        [FromQuery] string?   status = null,
        [FromQuery] DateTime? from   = null,
        [FromQuery] DateTime? to     = null,
        CancellationToken ct = default)
    {
        OrderStatus? parsedStatus = null;
        if (!string.IsNullOrEmpty(status) && Enum.TryParse<OrderStatus>(status, true, out var s))
            parsedStatus = s;

        var bytes = await ExportOrdersCsv.ExecuteAsync(db, parsedStatus, from, to, ct);

        var filename = $"orders_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";
        logger.LogInformation("Admin xuất CSV đơn hàng: {Filename} ({Bytes} bytes)", filename, bytes.Length);

        return File(bytes, "text/csv; charset=utf-8", filename);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TOKEN USAGE
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Thống kê sử dụng token:
    /// top 10 học sinh dùng nhiều nhất, top 10 chi nhiều nhất,
    /// số ví active / expired / zero-balance, avg token mỗi học sinh.
    /// </summary>
    [HttpGet("token-usage")]
    public async Task<IActionResult> GetTokenUsage(CancellationToken ct = default)
    {
        var result = await GetTokenUsageStats.ExecuteAsync(db, ct);
        return Ok(result);
    }
}
