using System.Text;
using Chatbot.Application.Common.Interfaces;
using Chatbot.Domain.Entities;
using Chatbot.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Chatbot.Application.Features.Payment;

// ─────────────────────────────────────────────────────────────────────────────
//  BÁO CÁO DOANH THU — Admin Revenue Report
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// KPI tổng quan doanh thu:
///   - Tổng / tháng này / tháng trước + tăng trưởng %
///   - Số đơn (paid / pending / failed)
///   - Số học sinh, ví, tỉ lệ chuyển đổi
///   - Token phát hành / tiêu thụ / còn lại + tỉ lệ tiêu thụ
///   - Top 5 gói theo doanh thu + % thị phần
/// </summary>
public static class GetRevenueSummary
{
    public sealed record Result(
        // Doanh thu
        decimal TotalRevenue,
        decimal RevenueThisMonth,
        decimal RevenueLastMonth,
        double  MonthOverMonthGrowthPct,
        // Đơn hàng
        int TotalOrders,
        int OrdersThisMonth,
        int PendingOrders,
        int FailedOrders,
        // Học sinh & ví
        int    TotalStudentsWithWallet,
        int    ActiveWallets,
        int    ExpiredWallets,
        double ConversionRatePct,           // % học sinh đã mua ít nhất 1 gói
        // Token
        long   TotalTokensIssued,
        long   TotalTokensConsumed,
        long   TotalTokensRemaining,
        double TokenConsumptionRatePct,
        // Top gói
        IReadOnlyList<PackageRevenueSummary> TopPackages);

    public sealed record PackageRevenueSummary(
        long     PackageId,
        string   PackageName,
        decimal  Price,
        int      TokenAmount,
        int      OrderCount,
        decimal  Revenue,
        long     TokensIssued,
        double   RevenueSharePct);

    public static async Task<Result> ExecuteAsync(
        IPaymentDbContext db, IAppDbContext appDb, CancellationToken ct = default)
    {
        var now             = DateTime.UtcNow;
        var thisMonthStart  = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var lastMonthStart  = thisMonthStart.AddMonths(-1);

        var paidOrders = db.StudentTokenOrders.Where(o => o.Status == OrderStatus.Paid);

        var totalRevenue    = await paidOrders.SumAsync(o => (decimal?)o.AmountPaid, ct) ?? 0m;
        var totalOrders     = await paidOrders.CountAsync(ct);
        var pendingOrders   = await db.StudentTokenOrders.CountAsync(o => o.Status == OrderStatus.Pending, ct);
        var failedOrders    = await db.StudentTokenOrders.CountAsync(o => o.Status == OrderStatus.Failed, ct);

        var revenueThisMonth = await paidOrders
            .Where(o => o.PaidAtUtc >= thisMonthStart)
            .SumAsync(o => (decimal?)o.AmountPaid, ct) ?? 0m;

        var ordersThisMonth = await paidOrders
            .Where(o => o.PaidAtUtc >= thisMonthStart)
            .CountAsync(ct);

        var revenueLastMonth = await paidOrders
            .Where(o => o.PaidAtUtc >= lastMonthStart && o.PaidAtUtc < thisMonthStart)
            .SumAsync(o => (decimal?)o.AmountPaid, ct) ?? 0m;

        var growthPct = revenueLastMonth == 0
            ? (revenueThisMonth > 0 ? 100.0 : 0.0)
            : (double)((revenueThisMonth - revenueLastMonth) / revenueLastMonth * 100);

        // Ví
        var wallets = await db.StudentTokenWallets
            .Select(w => new { w.AvailableTokens, w.UsedTokens, w.ExpiresAtUtc })
            .ToListAsync(ct);

        var totalStudentsWithWallet = wallets.Count;
        var activeWallets  = wallets.Count(w => w.ExpiresAtUtc == null || w.ExpiresAtUtc > now);
        var expiredWallets = wallets.Count(w => w.ExpiresAtUtc.HasValue && w.ExpiresAtUtc <= now);

        // Tỉ lệ chuyển đổi (học sinh đã mua ít nhất 1 gói / tổng học sinh)
        var totalStudents = await appDb.Users
            .Where(u => u.UserRoles.Any(ur => ur.Role.NormalizedName == "STUDENT"))
            .CountAsync(ct);
        var conversionPct = totalStudents == 0 ? 0.0
            : Math.Round((double)totalStudentsWithWallet / totalStudents * 100, 2);

        // Token aggregates (tính từ wallet để đảm bảo consistent)
        var totalIssued    = wallets.Sum(w => (long)(w.AvailableTokens + w.UsedTokens));
        var totalConsumed  = wallets.Sum(w => (long)w.UsedTokens);
        var totalRemaining = wallets.Sum(w => (long)w.AvailableTokens);
        var consumptionPct = totalIssued == 0 ? 0.0
            : Math.Round((double)totalConsumed / totalIssued * 100, 2);

        // Top gói theo doanh thu
        var packageData = await db.StudentTokenOrders
            .Where(o => o.Status == OrderStatus.Paid)
            .GroupBy(o => new { o.PackageId, o.Package.Name, o.Package.Price, o.Package.TokenAmount })
            .Select(g => new
            {
                g.Key.PackageId,
                g.Key.Name,
                g.Key.Price,
                g.Key.TokenAmount,
                OrderCount = g.Count(),
                Revenue    = g.Sum(o => o.AmountPaid),
                Issued     = (long)g.Sum(o => o.TokenAmount),
            })
            .OrderByDescending(p => p.Revenue)
            .Take(5)
            .ToListAsync(ct);

        var topPackages = packageData.Select(p => new PackageRevenueSummary(
            p.PackageId, p.Name, p.Price, p.TokenAmount,
            p.OrderCount, p.Revenue, p.Issued,
            totalRevenue == 0 ? 0 : Math.Round((double)(p.Revenue / totalRevenue * 100), 2)
        )).ToList();

        return new Result(
            totalRevenue, revenueThisMonth, revenueLastMonth, Math.Round(growthPct, 2),
            totalOrders, ordersThisMonth, pendingOrders, failedOrders,
            totalStudentsWithWallet, activeWallets, expiredWallets, conversionPct,
            totalIssued, totalConsumed, totalRemaining, consumptionPct,
            topPackages);
    }
}

// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Doanh thu theo tháng trong năm — cho biểu đồ cột / đường (12 điểm).</summary>
public static class GetMonthlyRevenue
{
    public sealed record Query(int Year);
    public sealed record MonthlyData(
        int     Month,
        string  MonthName,
        decimal Revenue,
        int     Orders,
        int     NewStudents);   // số học sinh mua lần đầu trong tháng

    public static async Task<IReadOnlyList<MonthlyData>> ExecuteAsync(
        IPaymentDbContext db, Query q, CancellationToken ct = default)
    {
        var yearStart = new DateTime(q.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var yearEnd   = yearStart.AddYears(1);

        var orderData = await db.StudentTokenOrders
            .Where(o => o.Status == OrderStatus.Paid
                     && o.PaidAtUtc >= yearStart
                     && o.PaidAtUtc < yearEnd)
            .GroupBy(o => o.PaidAtUtc!.Value.Month)
            .Select(g => new { Month = g.Key, Revenue = g.Sum(o => o.AmountPaid), Orders = g.Count() })
            .ToListAsync(ct);

        // Học sinh tạo ví lần đầu trong từng tháng
        var newStudentData = await db.StudentTokenWallets
            .Where(w => w.CreatedAtUtc >= yearStart && w.CreatedAtUtc < yearEnd)
            .GroupBy(w => w.CreatedAtUtc.Month)
            .Select(g => new { Month = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var monthNames = new[]
        {
            "Tháng 1","Tháng 2","Tháng 3","Tháng 4",
            "Tháng 5","Tháng 6","Tháng 7","Tháng 8",
            "Tháng 9","Tháng 10","Tháng 11","Tháng 12"
        };

        return Enumerable.Range(1, 12).Select(m =>
        {
            var o = orderData.FirstOrDefault(r => r.Month == m);
            var s = newStudentData.FirstOrDefault(r => r.Month == m);
            return new MonthlyData(m, monthNames[m - 1], o?.Revenue ?? 0, o?.Orders ?? 0, s?.Count ?? 0);
        }).ToList();
    }
}

// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Doanh thu theo ngày trong khoảng thời gian — cho biểu đồ chi tiết (tối đa 90 ngày).</summary>
public static class GetDailyRevenue
{
    public sealed record Query(DateTime From, DateTime To);
    public sealed record DailyData(string Date, decimal Revenue, int Orders);

    public static async Task<IReadOnlyList<DailyData>> ExecuteAsync(
        IPaymentDbContext db, Query q, CancellationToken ct = default)
    {
        var from       = q.From.Date;
        var toDate     = q.To.Date > from.AddDays(90) ? from.AddDays(90) : q.To.Date;
        var fromUtc    = DateTime.SpecifyKind(from, DateTimeKind.Utc);
        var toExclUtc  = DateTime.SpecifyKind(toDate.AddDays(1), DateTimeKind.Utc);

        var raw = await db.StudentTokenOrders
            .Where(o => o.Status == OrderStatus.Paid
                     && o.PaidAtUtc >= fromUtc
                     && o.PaidAtUtc < toExclUtc)
            .Select(o => new { o.PaidAtUtc, o.AmountPaid })
            .ToListAsync(ct);

        var grouped = raw
            .GroupBy(o => o.PaidAtUtc!.Value.Date)
            .ToDictionary(g => g.Key, g => new { Revenue = g.Sum(x => x.AmountPaid), Count = g.Count() });

        var result = new List<DailyData>();
        for (var d = from; d <= toDate; d = d.AddDays(1))
        {
            grouped.TryGetValue(d, out var val);
            result.Add(new DailyData(d.ToString("yyyy-MM-dd"), val?.Revenue ?? 0, val?.Count ?? 0));
        }
        return result;
    }
}

// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Thống kê chi tiết từng gói token: doanh thu, số lần bán, token phát hành, avg/ngày.</summary>
public static class GetPackageStats
{
    public sealed record PackageStat(
        long    PackageId,
        string  Name,
        string? Description,
        decimal Price,
        int     TokenAmount,
        bool    IsActive,
        int     TotalOrders,
        int     PaidOrders,
        int     PendingOrders,
        int     FailedOrders,
        decimal TotalRevenue,
        long    TotalTokensIssued,
        decimal AvgRevenuePerDay);

    public static async Task<IReadOnlyList<PackageStat>> ExecuteAsync(
        IPaymentDbContext db, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        var packages = await db.TokenPackages
            .Select(p => new
            {
                p.Id, p.Name, p.Description, p.Price, p.TokenAmount, p.IsActive,
                p.CreatedAtUtc,
                TotalOrders   = p.Orders.Count,
                PaidOrders    = p.Orders.Count(o => o.Status == OrderStatus.Paid),
                PendingOrders = p.Orders.Count(o => o.Status == OrderStatus.Pending),
                FailedOrders  = p.Orders.Count(o => o.Status == OrderStatus.Failed),
                TotalRevenue  = p.Orders.Where(o => o.Status == OrderStatus.Paid).Sum(o => (decimal?)o.AmountPaid) ?? 0m,
                TokensIssued  = (long)p.Orders.Where(o => o.Status == OrderStatus.Paid).Sum(o => o.TokenAmount),
            })
            .OrderBy(p => p.Id)
            .ToListAsync(ct);

        return packages.Select(p =>
        {
            var days = Math.Max(1, (now - p.CreatedAtUtc).TotalDays);
            return new PackageStat(
                p.Id, p.Name, p.Description, p.Price, p.TokenAmount, p.IsActive,
                p.TotalOrders, p.PaidOrders, p.PendingOrders, p.FailedOrders,
                p.TotalRevenue, p.TokensIssued,
                Math.Round(p.TotalRevenue / (decimal)days, 0));
        }).ToList();
    }
}

// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Danh sách đơn hàng có filter + tìm kiếm + phân trang (Admin).</summary>
public static class ListOrders
{
    public sealed record Query(
        OrderStatus? Status    = null,
        long?        UserId    = null,
        long?        PackageId = null,
        DateTime?    FromDate  = null,
        DateTime?    ToDate    = null,
        string?      Search    = null,   // tìm theo OrderRef, email, họ tên
        int          Page      = 1,
        int          PageSize  = 20);

    public sealed record OrderDto(
        long      Id,
        string    OrderRef,
        long      UserId,
        string    UserFullName,
        string    UserEmail,
        long      PackageId,
        string    PackageName,
        decimal   AmountPaid,
        int       TokenAmount,
        string    Status,
        string?   VnpayTransactionId,
        string?   VnpayBankCode,
        string?   VnpayCardType,
        DateTime? PaidAtUtc,
        DateTime  CreatedAtUtc,
        DateTime  ExpiredAtUtc);

    public sealed record Result(IReadOnlyList<OrderDto> Items, int TotalCount, decimal TotalRevenue);

    public static async Task<Result> ExecuteAsync(
        IPaymentDbContext db, Query q, CancellationToken ct = default)
    {
        var query = db.StudentTokenOrders.AsQueryable();

        if (q.Status.HasValue)
            query = query.Where(o => o.Status == q.Status.Value);
        if (q.UserId.HasValue)
            query = query.Where(o => o.UserId == q.UserId.Value);
        if (q.PackageId.HasValue)
            query = query.Where(o => o.PackageId == q.PackageId.Value);
        if (q.FromDate.HasValue)
            query = query.Where(o => o.CreatedAtUtc >= q.FromDate.Value);
        if (q.ToDate.HasValue)
            query = query.Where(o => o.CreatedAtUtc <= q.ToDate.Value.AddDays(1));
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var kw = q.Search.Trim().ToLower();
            query = query.Where(o => o.OrderRef.ToLower().Contains(kw)
                                  || o.User.Email.ToLower().Contains(kw)
                                  || o.User.FullName.ToLower().Contains(kw));
        }

        var total        = await query.CountAsync(ct);
        var totalRevenue = await query
            .Where(o => o.Status == OrderStatus.Paid)
            .SumAsync(o => (decimal?)o.AmountPaid, ct) ?? 0m;

        var items = await query
            .OrderByDescending(o => o.CreatedAtUtc)
            .Skip((q.Page - 1) * q.PageSize)
            .Take(q.PageSize)
            .Select(o => new OrderDto(
                o.Id, o.OrderRef,
                o.UserId, o.User.FullName, o.User.Email,
                o.PackageId, o.Package.Name,
                o.AmountPaid, o.TokenAmount,
                o.Status.ToString(),
                o.VnpayTransactionId, o.VnpayBankCode, o.VnpayCardType,
                o.PaidAtUtc, o.CreatedAtUtc, o.ExpiredAtUtc))
            .ToListAsync(ct);

        return new Result(items, total, totalRevenue);
    }
}

// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Lịch sử mua gói + giao dịch token của 1 học sinh (Admin xem).</summary>
public static class GetStudentPurchaseHistory
{
    public sealed record Query(long UserId);

    public sealed record StudentPurchaseHistory(
        long      UserId,
        string    FullName,
        string    Email,
        int       AvailableTokens,
        int       UsedTokens,
        DateTime? WalletExpiresAtUtc,
        decimal   TotalSpent,
        int       TotalOrders,
        IReadOnlyList<ListOrders.OrderDto>  Orders,
        IReadOnlyList<TransactionRow>       RecentTransactions);

    public sealed record TransactionRow(
        long     Id,
        string   Type,
        int      Delta,
        int      BalanceAfter,
        string?  Description,
        DateTime CreatedAtUtc);

    public static async Task<StudentPurchaseHistory?> ExecuteAsync(
        IPaymentDbContext db, Query q, CancellationToken ct = default)
    {
        var walletInfo = await db.StudentTokenWallets
            .Where(w => w.UserId == q.UserId)
            .Select(w => new { w.Id, w.AvailableTokens, w.UsedTokens, w.ExpiresAtUtc,
                               w.User.FullName, w.User.Email })
            .FirstOrDefaultAsync(ct);

        if (walletInfo is null) return null;

        var totalSpent = await db.StudentTokenOrders
            .Where(o => o.UserId == q.UserId && o.Status == OrderStatus.Paid)
            .SumAsync(o => (decimal?)o.AmountPaid, ct) ?? 0m;

        var orders = await db.StudentTokenOrders
            .Where(o => o.UserId == q.UserId)
            .OrderByDescending(o => o.CreatedAtUtc)
            .Select(o => new ListOrders.OrderDto(
                o.Id, o.OrderRef,
                o.UserId, o.User.FullName, o.User.Email,
                o.PackageId, o.Package.Name,
                o.AmountPaid, o.TokenAmount,
                o.Status.ToString(),
                o.VnpayTransactionId, o.VnpayBankCode, o.VnpayCardType,
                o.PaidAtUtc, o.CreatedAtUtc, o.ExpiredAtUtc))
            .ToListAsync(ct);

        var txs = await db.TokenTransactions
            .Where(t => t.WalletId == walletInfo.Id)
            .OrderByDescending(t => t.CreatedAtUtc)
            .Take(50)
            .Select(t => new TransactionRow(
                t.Id, t.Type.ToString(), t.Delta, t.BalanceAfter, t.Description, t.CreatedAtUtc))
            .ToListAsync(ct);

        return new StudentPurchaseHistory(
            q.UserId, walletInfo.FullName, walletInfo.Email,
            walletInfo.AvailableTokens, walletInfo.UsedTokens, walletInfo.ExpiresAtUtc,
            totalSpent, orders.Count, orders, txs);
    }
}

// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Thống kê token: top học sinh dùng nhiều / chi nhiều, ví active/expired.</summary>
public static class GetTokenUsageStats
{
    public sealed record TopUser(
        long    UserId,
        string  FullName,
        string  Email,
        int     AvailableTokens,
        int     UsedTokens,
        decimal TotalSpent);

    public sealed record Result(
        IReadOnlyList<TopUser> TopByUsage,
        IReadOnlyList<TopUser> TopBySpend,
        int    ActiveWallets,
        int    ExpiredWallets,
        int    ZeroBalanceWallets,
        double AvgTokensPerStudent);

    public static async Task<Result> ExecuteAsync(
        IPaymentDbContext db, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        var wallets = await db.StudentTokenWallets
            .Select(w => new
            {
                w.UserId,
                w.User.FullName,
                w.User.Email,
                w.AvailableTokens,
                w.UsedTokens,
                w.ExpiresAtUtc,
                TotalSpent = db.StudentTokenOrders
                    .Where(o => o.UserId == w.UserId && o.Status == OrderStatus.Paid)
                    .Sum(o => (decimal?)o.AmountPaid) ?? 0m,
            })
            .ToListAsync(ct);

        var topByUsage = wallets
            .OrderByDescending(w => w.UsedTokens).Take(10)
            .Select(w => new TopUser(w.UserId, w.FullName, w.Email, w.AvailableTokens, w.UsedTokens, w.TotalSpent))
            .ToList();

        var topBySpend = wallets
            .OrderByDescending(w => w.TotalSpent).Take(10)
            .Select(w => new TopUser(w.UserId, w.FullName, w.Email, w.AvailableTokens, w.UsedTokens, w.TotalSpent))
            .ToList();

        var active      = wallets.Count(w => w.ExpiresAtUtc == null || w.ExpiresAtUtc > now);
        var expired     = wallets.Count(w => w.ExpiresAtUtc.HasValue && w.ExpiresAtUtc <= now);
        var zeroBalance = wallets.Count(w => w.AvailableTokens == 0);
        var avgTokens   = wallets.Count == 0 ? 0.0
            : Math.Round(wallets.Average(w => (double)w.AvailableTokens), 1);

        return new Result(topByUsage, topBySpend, active, expired, zeroBalance, avgTokens);
    }
}

// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Admin hoàn tiền đơn hàng đã thanh toán → Refunded + hoàn token về ví.</summary>
public static class RefundOrder
{
    public sealed record Command(long OrderId, string Reason);
    public sealed record Result(bool Success, string Message);

    public static async Task<Result> ExecuteAsync(
        IPaymentDbContext db, Command cmd, CancellationToken ct = default)
    {
        var order = await db.StudentTokenOrders
            .FirstOrDefaultAsync(o => o.Id == cmd.OrderId, ct);

        if (order is null)
            return new Result(false, $"Không tìm thấy đơn #{cmd.OrderId}.");

        if (order.Status != OrderStatus.Paid)
            return new Result(false, $"Chỉ hoàn tiền được đơn đã thanh toán. Trạng thái: {order.Status}.");

        order.Status = OrderStatus.Refunded;

        var wallet = await db.StudentTokenWallets
            .FirstOrDefaultAsync(w => w.UserId == order.UserId, ct);

        if (wallet is not null)
        {
            var returnTokens = Math.Min(order.TokenAmount, wallet.AvailableTokens);
            wallet.AvailableTokens -= returnTokens;
            wallet.UsedTokens = Math.Max(0, wallet.UsedTokens - (order.TokenAmount - returnTokens));

            db.TokenTransactions.Add(new TokenTransaction
            {
                WalletId     = wallet.Id,
                UserId       = order.UserId,
                Type         = TokenTransactionType.Refund,
                Delta        = -order.TokenAmount,
                BalanceAfter = wallet.AvailableTokens,
                Description  = $"[Hoàn tiền] {cmd.Reason} — Đơn {order.OrderRef}",
                OrderId      = order.Id,
            });
        }

        await db.SaveChangesAsync(ct);
        return new Result(true, $"Đã hoàn tiền đơn {order.OrderRef} thành công.");
    }
}

// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Xuất danh sách đơn hàng ra file CSV (UTF-8 BOM — Excel đọc được tiếng Việt).</summary>
public static class ExportOrdersCsv
{
    public static async Task<byte[]> ExecuteAsync(
        IPaymentDbContext db,
        OrderStatus? status,
        DateTime? from,
        DateTime? to,
        CancellationToken ct = default)
    {
        var query = db.StudentTokenOrders.AsQueryable();
        if (status.HasValue)  query = query.Where(o => o.Status == status.Value);
        if (from.HasValue)    query = query.Where(o => o.CreatedAtUtc >= from.Value);
        if (to.HasValue)      query = query.Where(o => o.CreatedAtUtc <= to.Value.AddDays(1));

        var rows = await query
            .OrderByDescending(o => o.CreatedAtUtc)
            .Select(o => new
            {
                o.Id, o.OrderRef,
                UserEmail    = o.User.Email,
                UserFullName = o.User.FullName,
                PackageName  = o.Package.Name,
                o.AmountPaid,
                o.TokenAmount,
                Status       = o.Status.ToString(),
                o.VnpayTransactionId,
                o.VnpayBankCode,
                o.PaidAtUtc,
                o.CreatedAtUtc,
            })
            .ToListAsync(ct);

        var sb = new StringBuilder();
        sb.AppendLine("ID,Mã đơn,Email học sinh,Họ tên,Tên gói,Số tiền (đ),Số token,Trạng thái,Mã GD VnPay,Ngân hàng,Thời điểm thanh toán,Thời điểm tạo");

        TimeZoneInfo? vnTz = null;
        try { vnTz = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"); } catch { }

        string ToLocal(DateTime utc) => vnTz is null
            ? utc.ToString("dd/MM/yyyy HH:mm:ss")
            : TimeZoneInfo.ConvertTimeFromUtc(utc, vnTz).ToString("dd/MM/yyyy HH:mm:ss");

        foreach (var r in rows)
        {
            sb.AppendLine(string.Join(",",
                r.Id,
                r.OrderRef,
                Q(r.UserEmail),
                Q(r.UserFullName),
                Q(r.PackageName),
                r.AmountPaid.ToString("F0"),
                r.TokenAmount,
                r.Status,
                r.VnpayTransactionId ?? "",
                r.VnpayBankCode ?? "",
                r.PaidAtUtc.HasValue ? ToLocal(r.PaidAtUtc.Value) : "",
                ToLocal(r.CreatedAtUtc)));
        }

        // UTF-8 BOM để Excel nhận dạng đúng tiếng Việt
        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(sb.ToString())];
    }

    private static string Q(string? s) =>
        s is null ? "" : $"\"{s.Replace("\"", "\"\"")}\"";
}
