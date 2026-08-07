using Chatbot.Application.Common.Interfaces;
using Chatbot.Domain.Entities;
using Chatbot.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Chatbot.Application.Features.Payment;

// ─────────────────────────────────────────────────────────────────────────────
//  TOKEN PACKAGE — CRUD cho Admin
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Lấy danh sách tất cả gói token (Admin).</summary>
public static class ListTokenPackages
{
    public sealed record Query(bool IncludeInactive = false);

    public sealed record Result(
        long Id,
        string Name,
        string? Description,
        int TokenAmount,
        decimal Price,
        int? ValidityDays,
        bool IsActive,
        int DisplayOrder,
        int TotalOrders,
        DateTime CreatedAtUtc);

    public static async Task<IReadOnlyList<Result>> ExecuteAsync(
        IPaymentDbContext db, Query q, CancellationToken ct = default)
    {
        var query = db.TokenPackages.AsQueryable();
        if (!q.IncludeInactive)
            query = query.Where(p => p.IsActive);

        return await query
            .OrderBy(p => p.DisplayOrder)
            .Select(p => new Result(
                p.Id, p.Name, p.Description, p.TokenAmount, p.Price,
                p.ValidityDays, p.IsActive, p.DisplayOrder,
                p.Orders.Count,
                p.CreatedAtUtc))
            .ToListAsync(ct);
    }
}

/// <summary>Tạo gói token mới (Admin).</summary>
public static class CreateTokenPackage
{
    public sealed record Command(
        string Name,
        string? Description,
        int TokenAmount,
        decimal Price,
        int? ValidityDays,
        int DisplayOrder = 0);

    public sealed record Result(long Id);

    public static async Task<Result> ExecuteAsync(
        IPaymentDbContext db, ICurrentUser currentUser, Command cmd, CancellationToken ct = default)
    {
        var package = new TokenPackage
        {
            Name = cmd.Name.Trim(),
            Description = cmd.Description?.Trim(),
            TokenAmount = cmd.TokenAmount,
            Price = cmd.Price,
            ValidityDays = cmd.ValidityDays,
            DisplayOrder = cmd.DisplayOrder,
            IsActive = true,
        };
        db.TokenPackages.Add(package);
        await db.SaveChangesAsync(ct);
        return new Result(package.Id);
    }
}

/// <summary>Cập nhật gói token (Admin).</summary>
public static class UpdateTokenPackage
{
    public sealed record Command(
        long Id,
        string Name,
        string? Description,
        int TokenAmount,
        decimal Price,
        int? ValidityDays,
        bool IsActive,
        int DisplayOrder);

    public static async Task ExecuteAsync(
        IPaymentDbContext db, Command cmd, CancellationToken ct = default)
    {
        var package = await db.TokenPackages.FindAsync([cmd.Id], ct)
            ?? throw new KeyNotFoundException($"Không tìm thấy gói token #{cmd.Id}.");

        package.Name = cmd.Name.Trim();
        package.Description = cmd.Description?.Trim();
        package.TokenAmount = cmd.TokenAmount;
        package.Price = cmd.Price;
        package.ValidityDays = cmd.ValidityDays;
        package.IsActive = cmd.IsActive;
        package.DisplayOrder = cmd.DisplayOrder;

        await db.SaveChangesAsync(ct);
    }
}

/// <summary>Xoá (ẩn) gói token (Admin soft-deactivate).</summary>
public static class DeactivateTokenPackage
{
    public static async Task ExecuteAsync(
        IPaymentDbContext db, long packageId, CancellationToken ct = default)
    {
        var package = await db.TokenPackages.FindAsync([packageId], ct)
            ?? throw new KeyNotFoundException($"Không tìm thấy gói token #{packageId}.");
        package.IsActive = false;
        await db.SaveChangesAsync(ct);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
//  ORDER — Học sinh tạo đơn & nhận callback
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Học sinh tạo đơn mua gói token → nhận URL thanh toán VnPay.</summary>
public static class CreateTokenOrder
{
    public sealed record Command(long PackageId, string ReturnUrl, string ClientIp);
    public sealed record Result(long OrderId, string OrderRef, string PaymentUrl);

    public static async Task<Result> ExecuteAsync(
        IPaymentDbContext db,
        ICurrentUser currentUser,
        IVnPayService vnPay,
        Command cmd,
        CancellationToken ct = default)
    {
        var package = await db.TokenPackages
            .FirstOrDefaultAsync(p => p.Id == cmd.PackageId && p.IsActive, ct)
            ?? throw new KeyNotFoundException($"Gói token #{cmd.PackageId} không tồn tại hoặc đã ngưng bán.");

        // Cho phép nhiều đơn chờ (Pending) cùng gói cùng lúc;
        // mỗi lần tạo đơn là một session thanh toán VnPay độc lập.
        var orderRef = $"ORD-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..8].ToUpper()}";

        var order = new StudentTokenOrder
        {
            UserId = currentUser.UserId!.Value,
            PackageId = package.Id,
            AmountPaid = package.Price,
            TokenAmount = package.TokenAmount,
            Status = OrderStatus.Pending,
            OrderRef = orderRef,
            ExpiredAtUtc = DateTime.UtcNow.AddMinutes(15),
        };
        db.StudentTokenOrders.Add(order);
        await db.SaveChangesAsync(ct);

        var paymentUrl = vnPay.CreatePaymentUrl(
            order.Id,
            orderRef,
            package.Price,
            $"Mua {package.Name} – {package.TokenAmount} token",
            cmd.ReturnUrl,
            cmd.ClientIp);

        return new Result(order.Id, orderRef, paymentUrl);
    }
}

/// <summary>Học sinh xem danh sách đơn hàng mua gói token của chính mình.</summary>
public static class GetMyTokenOrders
{
    public sealed record Query(
        long UserId,
        OrderStatus? Status = null,
        int Page = 1,
        int PageSize = 20);

    public sealed record OrderDto(
        long Id,
        string OrderRef,
        long PackageId,
        string PackageName,
        decimal AmountPaid,
        int TokenAmount,
        string Status,
        string? VnpayTransactionId,
        string? VnpayBankCode,
        string? VnpayCardType,
        DateTime? PaidAtUtc,
        DateTime CreatedAtUtc,
        DateTime ExpiredAtUtc);

    public sealed record Result(IReadOnlyList<OrderDto> Items, int TotalCount);

    public static async Task<Result> ExecuteAsync(
        IPaymentDbContext db, Query q, CancellationToken ct = default)
    {
        var query = db.StudentTokenOrders
            .Where(o => o.UserId == q.UserId);

        if (q.Status.HasValue)
            query = query.Where(o => o.Status == q.Status.Value);

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(o => o.CreatedAtUtc)
            .Skip((q.Page - 1) * q.PageSize)
            .Take(q.PageSize)
            .Select(o => new OrderDto(
                o.Id, o.OrderRef,
                o.PackageId, o.Package.Name,
                o.AmountPaid, o.TokenAmount,
                o.Status.ToString(),
                o.VnpayTransactionId, o.VnpayBankCode, o.VnpayCardType,
                o.PaidAtUtc, o.CreatedAtUtc, o.ExpiredAtUtc))
            .ToListAsync(ct);

        return new Result(items, total);
    }
}

/// <summary>
/// Xử lý callback từ VnPay (IPN hoặc Return URL).
/// Cộng token vào ví nếu thanh toán thành công.
/// </summary>
public static class ProcessVnPayCallback
{
    public sealed record Command(IDictionary<string, string> QueryParams);
    public sealed record Result(bool Success, string Message, long? OrderId, int? TokensAdded);

    public static async Task<Result> ExecuteAsync(
        IPaymentDbContext db,
        IVnPayService vnPay,
        IEmailService email,
        ILogger logger,
        Command cmd,
        CancellationToken ct = default)
    {
        var callbackResult = vnPay.ValidateCallback(cmd.QueryParams);

        if (!callbackResult.IsValidSignature)
            return new Result(false, "Chữ ký không hợp lệ.", null, null);

        var order = await db.StudentTokenOrders
            .FirstOrDefaultAsync(o => o.OrderRef == callbackResult.OrderRef, ct);

        if (order is null)
            return new Result(false, $"Không tìm thấy đơn hàng {callbackResult.OrderRef}.", null, null);

        // Ghi response VnPay vào order
        order.VnpayTransactionId = callbackResult.TransactionId;
        order.VnpayResponseCode = callbackResult.ResponseCode;
        order.VnpayBankCode = callbackResult.BankCode;
        order.VnpayCardType = callbackResult.CardType;
        order.VnpayRawResponse = callbackResult.RawResponse;

        // Idempotent: nếu đã xử lý rồi thì skip
        if (order.Status == OrderStatus.Paid)
            return new Result(true, "Đơn hàng đã được xử lý trước đó.", order.Id, null);

        if (callbackResult.ResponseCode != "00")
        {
            order.Status = OrderStatus.Failed;
            await db.SaveChangesAsync(ct);
            return new Result(false, $"Thanh toán thất bại. Mã lỗi VnPay: {callbackResult.ResponseCode}.", order.Id, null);
        }

        if (order.Status == OrderStatus.Expired)
        {
            order.Status = OrderStatus.Failed;
            await db.SaveChangesAsync(ct);
            return new Result(false, "Đơn hàng đã hết hạn.", order.Id, null);
        }

        // ✅ Thanh toán thành công → cập nhật order
        order.Status = OrderStatus.Paid;
        order.PaidAtUtc = callbackResult.PayDate;

        // Cộng token vào ví
        var wallet = await db.StudentTokenWallets
            .FirstOrDefaultAsync(w => w.UserId == order.UserId, ct);

        if (wallet is null)
        {
            wallet = new StudentTokenWallet { UserId = order.UserId };
            db.StudentTokenWallets.Add(wallet);
        }

        wallet.AvailableTokens += order.TokenAmount;

        // Token cộng dồn vĩnh viễn — không đặt ngày hết hạn ví.
        // Mỗi lần mua thêm gói, số token được cộng thẳng vào AvailableTokens
        // mà không bị giới hạn bởi ValidityDays của gói.
        var package = await db.TokenPackages.FindAsync([order.PackageId], ct);
        wallet.ExpiresAtUtc = null;

        // Ghi lịch sử giao dịch
        var tx = new TokenTransaction
        {
            UserId = order.UserId,
            Type = TokenTransactionType.Purchase,
            Delta = order.TokenAmount,
            BalanceAfter = wallet.AvailableTokens,
            Description = $"Mua gói {package?.Name ?? "N/A"}",
            OrderId = order.Id,
        };
        wallet.Transactions.Add(tx);

        await db.SaveChangesAsync(ct);

        // ── Gửi email xác nhận mua gói (fire-and-forget, không block flow thanh toán) ──
        _ = SendPurchaseConfirmationEmailAsync(
            email, logger, order, package, wallet, ct);

        return new Result(true, "Thanh toán thành công.", order.Id, order.TokenAmount);
    }

    // ─── Email template xác nhận mua gói ────────────────────────────────────────
    private static async Task SendPurchaseConfirmationEmailAsync(
        IEmailService email,
        ILogger logger,
        StudentTokenOrder order,
        TokenPackage? package,
        StudentTokenWallet wallet,
        CancellationToken ct)
    {
        try
        {
            // Lấy email & tên học sinh qua navigation property (đã được load)
            var toEmail   = order.User?.Email;
            var fullName  = order.User?.FullName ?? "Học sinh";
            if (string.IsNullOrWhiteSpace(toEmail)) return;

            var packageName  = package?.Name ?? "N/A";
            var tokenAmount  = order.TokenAmount;
            var amountPaid   = order.AmountPaid.ToString("N0");
            var expiry       = wallet.ExpiresAtUtc.HasValue
                ? wallet.ExpiresAtUtc.Value.AddHours(7).ToString("dd/MM/yyyy HH:mm")
                : "Không giới hạn";
            var paidAt       = (order.PaidAtUtc ?? DateTime.UtcNow).AddHours(7).ToString("dd/MM/yyyy HH:mm:ss");
            var balanceAfter = wallet.AvailableTokens;

            var subject = $"✅ Xác nhận mua gói thành công — {packageName}";
            var html = $"""
                <!DOCTYPE html>
                <html lang="vi">
                <head><meta charset="UTF-8"><meta name="viewport" content="width=device-width,initial-scale=1">
                <title>Xác nhận mua gói</title></head>
                <body style="margin:0;padding:0;background:#f4f6f9;font-family:'Segoe UI',Arial,sans-serif">
                  <table width="100%" cellpadding="0" cellspacing="0" style="background:#f4f6f9;padding:40px 0">
                    <tr><td align="center">
                      <table width="600" cellpadding="0" cellspacing="0" style="background:#ffffff;border-radius:12px;overflow:hidden;box-shadow:0 4px 24px rgba(0,0,0,.08)">

                        <!-- HEADER -->
                        <tr>
                          <td style="background:linear-gradient(135deg,#6366f1 0%,#8b5cf6 100%);padding:40px 48px;text-align:center">
                            <p style="margin:0 0 8px;color:rgba(255,255,255,.8);font-size:14px;letter-spacing:2px;text-transform:uppercase">Chatbot Học Tập</p>
                            <h1 style="margin:0;color:#ffffff;font-size:28px;font-weight:700">Thanh toán thành công! 🎉</h1>
                          </td>
                        </tr>

                        <!-- BODY -->
                        <tr>
                          <td style="padding:40px 48px">
                            <p style="margin:0 0 8px;color:#374151;font-size:16px">Xin chào <strong>{fullName}</strong>,</p>
                            <p style="margin:0 0 28px;color:#6b7280;font-size:15px;line-height:1.6">
                              Gói token của bạn đã được kích hoạt thành công. Bạn có thể bắt đầu đặt câu hỏi với AI ngay bây giờ!
                            </p>

                            <!-- ORDER CARD -->
                            <table width="100%" cellpadding="0" cellspacing="0"
                              style="background:#f8f7ff;border:1px solid #e5e7eb;border-radius:10px;margin-bottom:28px">
                              <tr><td style="padding:24px 28px">
                                <p style="margin:0 0 16px;font-size:13px;font-weight:600;color:#8b5cf6;letter-spacing:1.5px;text-transform:uppercase">Chi tiết đơn hàng</p>
                                <table width="100%" cellpadding="0" cellspacing="0">
                                  {Row("Gói mua",         packageName)}
                                  {Row("Số token nhận",   $"{tokenAmount:N0} token")}
                                  {Row("Số tiền thanh toán", $"{amountPaid}₫")}
                                  {Row("Mã đơn hàng",     order.OrderRef)}
                                  {Row("Thời gian TT",    paidAt)}
                                  {Row("Hiệu lực đến",    expiry)}
                                </table>
                              </td></tr>
                            </table>

                            <!-- BALANCE BANNER -->
                            <table width="100%" cellpadding="0" cellspacing="0"
                              style="background:linear-gradient(135deg,#6366f1,#8b5cf6);border-radius:10px;margin-bottom:32px">
                              <tr><td style="padding:20px 28px;text-align:center">
                                <p style="margin:0 0 4px;color:rgba(255,255,255,.8);font-size:13px">Số token hiện có trong ví</p>
                                <p style="margin:0;color:#ffffff;font-size:36px;font-weight:800">{balanceAfter:N0} <span style="font-size:18px;font-weight:400">token</span></p>
                              </td></tr>
                            </table>

                            <p style="margin:0 0 28px;color:#6b7280;font-size:14px;line-height:1.6">
                              Mỗi câu hỏi bạn đặt cho AI sẽ tiêu thụ <strong>1 token</strong>.
                              Khi token về 0, bạn có thể mua thêm gói bất kỳ lúc nào.
                            </p>

                            <!-- CTA -->
                            <table width="100%" cellpadding="0" cellspacing="0">
                              <tr><td align="center">
                                <a href="#" style="display:inline-block;background:linear-gradient(135deg,#6366f1,#8b5cf6);color:#fff;text-decoration:none;padding:14px 40px;border-radius:8px;font-size:15px;font-weight:600">
                                  Bắt đầu học ngay →
                                </a>
                              </td></tr>
                            </table>
                          </td>
                        </tr>

                        <!-- FOOTER -->
                        <tr>
                          <td style="background:#f9fafb;padding:24px 48px;text-align:center;border-top:1px solid #f3f4f6">
                            <p style="margin:0;color:#9ca3af;font-size:12px;line-height:1.6">
                              Email này được gửi tự động, vui lòng không reply.<br>
                              Nếu bạn không thực hiện giao dịch này, hãy liên hệ quản trị viên ngay.
                            </p>
                          </td>
                        </tr>

                      </table>
                    </td></tr>
                  </table>
                </body></html>
                """;

            await email.SendEmailAsync(toEmail, subject, html);
            logger.LogInformation(
                "[Payment] Email xác nhận gửi tới {Email} — đơn {OrderRef}.",
                toEmail, order.OrderRef);
        }
        catch (Exception ex)
        {
            // Email lỗi không được làm fail flow thanh toán
            logger.LogWarning(ex,
                "[Payment] Không gửi được email xác nhận cho đơn {OrderRef}.", order.OrderRef);
        }
    }

    private static string Row(string label, string value) =>
        $"""
        <tr>
          <td style="padding:6px 0;color:#6b7280;font-size:14px;width:50%">{label}</td>
          <td style="padding:6px 0;color:#111827;font-size:14px;font-weight:600;text-align:right">{value}</td>
        </tr>
        """;
}

// ─────────────────────────────────────────────────────────────────────────────
//  WALLET — Kiểm tra & sử dụng token
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Lấy thông tin ví token của học sinh hiện tại.</summary>
public static class GetMyWallet
{
    public sealed record Result(
        long WalletId,
        int AvailableTokens,
        int UsedTokens,
        DateTime? ExpiresAtUtc,
        bool IsExpired);

    public static async Task<Result?> ExecuteAsync(
        IPaymentDbContext db, ICurrentUser currentUser, CancellationToken ct = default)
    {
        var wallet = await db.StudentTokenWallets
            .FirstOrDefaultAsync(w => w.UserId == currentUser.UserId, ct);

        if (wallet is null) return null;

        return new Result(
            wallet.Id,
            wallet.AvailableTokens,
            wallet.UsedTokens,
            wallet.ExpiresAtUtc,
            wallet.ExpiresAtUtc.HasValue && wallet.ExpiresAtUtc < DateTime.UtcNow);
    }
}

/// <summary>
/// Trừ token khi học sinh dùng chatbot.
/// Gọi từ ChatController hoặc ChatHub sau mỗi tin nhắn.
/// </summary>
public static class ConsumeToken
{
    public sealed record Command(long UserId, int Amount, string Description, long? ChatMessageId = null);
    public sealed record Result(bool Allowed, int RemainingTokens, string? Reason);

    public static async Task<Result> ExecuteAsync(
        IPaymentDbContext db, Command cmd, CancellationToken ct = default)
    {
        var wallet = await db.StudentTokenWallets
            .FirstOrDefaultAsync(w => w.UserId == cmd.UserId, ct);

        if (wallet is null)
            return new Result(false, 0, "Chưa có ví token. Vui lòng mua gói để sử dụng chatbot.");

        // ExpiresAtUtc luôn null (token cộng dồn vĩnh viễn), không cần kiểm tra hết hạn.

        if (wallet.AvailableTokens < cmd.Amount)
            return new Result(false, wallet.AvailableTokens, "Không đủ token. Vui lòng mua thêm gói.");

        wallet.AvailableTokens -= cmd.Amount;
        wallet.UsedTokens += cmd.Amount;

        var tx = new TokenTransaction
        {
            UserId = cmd.UserId,
            Type = TokenTransactionType.ChatUsage,
            Delta = -cmd.Amount,
            BalanceAfter = wallet.AvailableTokens,
            Description = cmd.Description,
            ChatMessageId = cmd.ChatMessageId,
        };
        wallet.Transactions.Add(tx);

        await db.SaveChangesAsync(ct);
        return new Result(true, wallet.AvailableTokens, null);
    }
}

/// <summary>Lịch sử giao dịch token của học sinh.</summary>
public static class GetTokenHistory
{
    public sealed record Query(long UserId, int Page = 1, int PageSize = 20);
    public sealed record TransactionDto(
        long Id,
        string Type,
        int Delta,
        int BalanceAfter,
        string? Description,
        DateTime CreatedAtUtc);
    public sealed record Result(IReadOnlyList<TransactionDto> Items, int TotalCount);

    public static async Task<Result> ExecuteAsync(
        IPaymentDbContext db, Query q, CancellationToken ct = default)
    {
        var wallet = await db.StudentTokenWallets
            .FirstOrDefaultAsync(w => w.UserId == q.UserId, ct);
        if (wallet is null) return new Result([], 0);

        var total = await db.TokenTransactions
            .Where(t => t.WalletId == wallet.Id)
            .CountAsync(ct);

        var items = await db.TokenTransactions
            .Where(t => t.WalletId == wallet.Id)
            .OrderByDescending(t => t.CreatedAtUtc)
            .Skip((q.Page - 1) * q.PageSize)
            .Take(q.PageSize)
            .Select(t => new TransactionDto(
                t.Id, t.Type.ToString(), t.Delta, t.BalanceAfter,
                t.Description, t.CreatedAtUtc))
            .ToListAsync(ct);

        return new Result(items, total);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
//  ADMIN ADJUST — Admin cộng/trừ token thủ công
// ─────────────────────────────────────────────────────────────────────────────

public static class AdminAdjustTokens
{
    public sealed record Command(long TargetUserId, int Delta, string Reason);

    public static async Task ExecuteAsync(
        IPaymentDbContext db, Command cmd, CancellationToken ct = default)
    {
        var wallet = await db.StudentTokenWallets
            .FirstOrDefaultAsync(w => w.UserId == cmd.TargetUserId, ct);

        if (wallet is null)
        {
            wallet = new StudentTokenWallet { UserId = cmd.TargetUserId };
            db.StudentTokenWallets.Add(wallet);
        }

        wallet.AvailableTokens += cmd.Delta;
        if (wallet.AvailableTokens < 0) wallet.AvailableTokens = 0;
        if (cmd.Delta > 0) wallet.UsedTokens -= Math.Min(cmd.Delta, wallet.UsedTokens);

        var tx = new TokenTransaction
        {
            UserId = cmd.TargetUserId,
            Type = TokenTransactionType.AdminAdjustment,
            Delta = cmd.Delta,
            BalanceAfter = wallet.AvailableTokens,
            Description = $"[Admin điều chỉnh] {cmd.Reason}",
        };
        wallet.Transactions.Add(tx);
        await db.SaveChangesAsync(ct);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
//  ADMIN REVOKE ORDER — Admin hủy gói mua của học sinh, thu hồi token
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Admin hủy đơn hàng đã thanh toán của học sinh và thu hồi token tương ứng khỏi ví.
/// Chỉ áp dụng với đơn có Status = Paid. Ghi lại lý do vào lịch sử giao dịch.
/// </summary>
public static class AdminRevokeOrder
{
    public sealed record Command(long OrderId, string Reason);
    public sealed record Result(long OrderId, string OrderRef, int TokensRevoked, int WalletBalanceAfter);

    public static async Task<Result> ExecuteAsync(
        IPaymentDbContext db, Command cmd, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(cmd.Reason))
            throw new ArgumentException("Cần ghi lý do thu hồi.", nameof(cmd.Reason));

        var order = await db.StudentTokenOrders
            .FirstOrDefaultAsync(o => o.Id == cmd.OrderId, ct)
            ?? throw new KeyNotFoundException($"Không tìm thấy đơn hàng #{cmd.OrderId}.");

        if (order.Status != OrderStatus.Paid)
            throw new InvalidOperationException(
                $"Chỉ có thể hủy đơn đã thanh toán (Paid). Trạng thái hiện tại: {order.Status}.");

        // Thu hồi token khỏi ví
        var wallet = await db.StudentTokenWallets
            .FirstOrDefaultAsync(w => w.UserId == order.UserId, ct)
            ?? throw new InvalidOperationException("Học sinh chưa có ví token.");

        var tokensToRevoke = Math.Min(order.TokenAmount, wallet.AvailableTokens);
        wallet.AvailableTokens -= tokensToRevoke;
        wallet.UsedTokens = Math.Max(0, wallet.UsedTokens - Math.Max(0, order.TokenAmount - tokensToRevoke));

        // Ghi lịch sử giao dịch
        var tx = new TokenTransaction
        {
            UserId = order.UserId,
            Type = TokenTransactionType.AdminAdjustment,
            Delta = -tokensToRevoke,
            BalanceAfter = wallet.AvailableTokens,
            Description = $"[Admin thu hồi đơn #{order.OrderRef}] {cmd.Reason}",
            OrderId = order.Id,
        };
        wallet.Transactions.Add(tx);

        // Đánh dấu đơn hàng là đã bị hủy
        order.Status = OrderStatus.Failed;

        await db.SaveChangesAsync(ct);

        return new Result(order.Id, order.OrderRef, tokensToRevoke, wallet.AvailableTokens);
    }
}
