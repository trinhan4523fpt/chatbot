using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Chatbot.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Chatbot.Infrastructure.Payment;

/// <summary>
/// Cấu hình VnPay — điền vào appsettings.json.
/// </summary>
public sealed class VnPayOptions
{
    public const string Section = "VnPay";

    /// <summary>Terminal ID (mã merchant) cấp bởi VnPay.</summary>
    public string TmnCode { get; set; } = string.Empty;

    /// <summary>Hash secret cấp bởi VnPay (dùng ký HMAC-SHA512).</summary>
    public string HashSecret { get; set; } = string.Empty;

    /// <summary>URL API của VnPay (sandbox hoặc production).</summary>
    public string BaseUrl { get; set; } = "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html";

    /// <summary>Phiên bản API VnPay.</summary>
    public string Version { get; set; } = "2.1.0";

    /// <summary>Múi giờ Việt Nam (+07:00).</summary>
    public string TimeZoneId { get; set; } = "SE Asia Standard Time";
}

/// <summary>
/// Implementation thực của IVnPayService — tích hợp cổng thanh toán VnPay v2.1.0.
///
/// Luồng hoạt động:
///   1. CreatePaymentUrl: Build SortedDictionary chứa các tham số vnp_*,
///      ký bằng HMAC-SHA512(HashSecret, queryString), trả về URL redirect.
///   2. ValidateCallback: Tách vnp_SecureHash ra, ký lại phần còn lại,
///      so sánh hash để xác thực chữ ký từ VnPay callback.
/// </summary>
public sealed class VnPayService : IVnPayService
{
    private readonly VnPayOptions _opts;
    private readonly ILogger<VnPayService> _logger;

    public VnPayService(IOptions<VnPayOptions> opts, ILogger<VnPayService> logger)
    {
        _opts = opts.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public string CreatePaymentUrl(
        long orderId,
        string orderRef,
        decimal amount,
        string orderInfo,
        string returnUrl,
        string ipAddress)
    {
        // Lấy thời gian Việt Nam (GMT+7)
        var vnTimeZone = GetVietnamTimeZone();
        var vnNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, vnTimeZone);

        // Build sorted params theo spec VnPay v2.1.0
        var vnpParams = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["vnp_Version"]    = _opts.Version,
            ["vnp_Command"]    = "pay",
            ["vnp_TmnCode"]    = _opts.TmnCode,
            ["vnp_Amount"]     = ((long)(amount * 100)).ToString(CultureInfo.InvariantCulture),
            ["vnp_CurrCode"]   = "VND",
            ["vnp_TxnRef"]     = orderRef,
            ["vnp_OrderInfo"]  = orderInfo,
            ["vnp_OrderType"]  = "other",
            ["vnp_Locale"]     = "vn",
            ["vnp_ReturnUrl"]  = returnUrl,
            ["vnp_IpAddr"]     = ipAddress,
            ["vnp_CreateDate"] = vnNow.ToString("yyyyMMddHHmmss"),
            ["vnp_ExpireDate"] = vnNow.AddMinutes(15).ToString("yyyyMMddHHmmss"),
        };

        // Build query string (URL-encode values, sorted by key)
        var queryString = BuildQueryString(vnpParams);

        // Ký HMAC-SHA512
        var secureHash = HmacSha512(_opts.HashSecret, queryString);

        var paymentUrl = $"{_opts.BaseUrl}?{queryString}&vnp_SecureHash={secureHash}";

        _logger.LogInformation(
            "[VnPay] Created payment URL for OrderRef={OrderRef}, Amount={Amount} VND.",
            orderRef, amount);

        return paymentUrl;
    }

    /// <inheritdoc />
    public VnPayCallbackResult ValidateCallback(IDictionary<string, string> queryParams)
    {
        // Lấy SecureHash từ callback
        queryParams.TryGetValue("vnp_SecureHash", out var receivedHash);

        // Build lại params để verify (bỏ vnp_SecureHash và vnp_SecureHashType)
        var sortedParams = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, value) in queryParams)
        {
            if (!string.IsNullOrEmpty(value)
                && !key.Equals("vnp_SecureHash", StringComparison.OrdinalIgnoreCase)
                && !key.Equals("vnp_SecureHashType", StringComparison.OrdinalIgnoreCase))
            {
                sortedParams[key] = value;
            }
        }

        // Ký lại và so sánh
        var queryString = BuildQueryString(sortedParams);
        var computedHash = HmacSha512(_opts.HashSecret, queryString);

        var isValidSignature = !string.IsNullOrEmpty(receivedHash)
            && string.Equals(computedHash, receivedHash, StringComparison.OrdinalIgnoreCase);

        // Parse các field từ callback
        queryParams.TryGetValue("vnp_ResponseCode", out var responseCode);
        queryParams.TryGetValue("vnp_TxnRef", out var orderRef);
        queryParams.TryGetValue("vnp_TransactionNo", out var txnId);
        queryParams.TryGetValue("vnp_BankCode", out var bankCode);
        queryParams.TryGetValue("vnp_CardType", out var cardType);
        queryParams.TryGetValue("vnp_Amount", out var amountStr);
        queryParams.TryGetValue("vnp_PayDate", out var payDateStr);

        var rawResponse = string.Join("&", queryParams
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => $"{kv.Key}={kv.Value}"));

        // Parse amount (VnPay trả về đã nhân 100)
        decimal.TryParse(amountStr ?? "0", NumberStyles.Integer, CultureInfo.InvariantCulture, out var amountRaw);

        // Parse pay date (format: yyyyMMddHHmmss)
        DateTime.TryParseExact(
            payDateStr ?? string.Empty,
            "yyyyMMddHHmmss",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var payDate);

        if (isValidSignature)
        {
            _logger.LogInformation(
                "[VnPay] Callback verified successfully. OrderRef={OrderRef}, ResponseCode={ResponseCode}.",
                orderRef, responseCode);
        }
        else
        {
            _logger.LogWarning(
                "[VnPay] Invalid signature for callback. OrderRef={OrderRef}, " +
                "ReceivedHash={ReceivedHash}, ComputedHash={ComputedHash}.",
                orderRef, receivedHash, computedHash);
        }

        return new VnPayCallbackResult(
            IsValidSignature: isValidSignature,
            OrderRef: orderRef ?? string.Empty,
            ResponseCode: responseCode ?? string.Empty,
            TransactionId: txnId ?? string.Empty,
            Amount: amountRaw / 100m,
            BankCode: bankCode ?? string.Empty,
            CardType: cardType ?? string.Empty,
            PayDate: payDate == default ? DateTime.UtcNow : payDate.ToUniversalTime(),
            RawResponse: rawResponse);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Private helpers
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Build query string từ SortedDictionary, URL-encode giá trị.
    /// Format: key1=value1&amp;key2=value2 (sorted by key).
    /// </summary>
    private static string BuildQueryString(SortedDictionary<string, string> parameters)
    {
        var sb = new StringBuilder();
        foreach (var (key, value) in parameters)
        {
            if (sb.Length > 0)
                sb.Append('&');

            sb.Append(key);
            sb.Append('=');
            sb.Append(WebUtility.UrlEncode(value));
        }
        return sb.ToString();
    }

    /// <summary>
    /// Tính HMAC-SHA512 theo chuẩn VnPay.
    /// Input: key = HashSecret, data = query string (đã sorted, không có SecureHash).
    /// Output: hex string (lowercase).
    /// </summary>
    private static string HmacSha512(string key, string data)
    {
        var keyBytes = Encoding.UTF8.GetBytes(key);
        var dataBytes = Encoding.UTF8.GetBytes(data);

        using var hmac = new HMACSHA512(keyBytes);
        var hashBytes = hmac.ComputeHash(dataBytes);

        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    /// <summary>
    /// Lấy TimeZoneInfo cho múi giờ Việt Nam.
    /// Hỗ trợ cả Windows ("SE Asia Standard Time") và Linux ("Asia/Ho_Chi_Minh").
    /// </summary>
    private TimeZoneInfo GetVietnamTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(_opts.TimeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            // Fallback cho Linux
            try { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh"); }
            catch { return TimeZoneInfo.CreateCustomTimeZone("VN", TimeSpan.FromHours(7), "Vietnam", "Vietnam Standard Time"); }
        }
    }
}
