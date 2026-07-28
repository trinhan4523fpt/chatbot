using Chatbot.Domain.Common;
using Chatbot.Domain.Enums;

namespace Chatbot.Domain.Entities;

/// <summary>
/// Gói token mà học sinh có thể mua để sử dụng chatbot.
/// Admin tạo và quản lý các gói này.
/// </summary>
public class TokenPackage : AuditableEntity
{
    /// <summary>Tên gói, ví dụ: "Gói Cơ Bản", "Gói Cao Cấp".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Mô tả chi tiết gói.</summary>
    public string? Description { get; set; }

    /// <summary>Số lượng token được cấp khi mua gói.</summary>
    public int TokenAmount { get; set; }

    /// <summary>Giá gói (VND).</summary>
    public decimal Price { get; set; }

    /// <summary>Số ngày có hiệu lực sau khi kích hoạt (null = không hết hạn).</summary>
    public int? ValidityDays { get; set; }

    /// <summary>Gói có đang được bán hay không.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Thứ tự hiển thị trong danh sách gói.</summary>
    public int DisplayOrder { get; set; }

    public ICollection<StudentTokenOrder> Orders { get; set; } = new List<StudentTokenOrder>();
}

/// <summary>
/// Đơn hàng mua gói token của học sinh.
/// Mỗi đơn hàng ánh xạ 1-1 với 1 giao dịch thanh toán VnPay.
/// </summary>
public class StudentTokenOrder : CreatedEntity
{
    public long UserId { get; set; }
    public long PackageId { get; set; }

    /// <summary>Giá tại thời điểm mua (snapshot — không bị ảnh hưởng nếu admin sửa gói).</summary>
    public decimal AmountPaid { get; set; }

    /// <summary>Số token được cấp (snapshot).</summary>
    public int TokenAmount { get; set; }

    public OrderStatus Status { get; set; } = OrderStatus.Pending;

    /// <summary>Mã đơn hàng nội bộ (unique), gửi lên VnPay làm vnp_TxnRef.</summary>
    public string OrderRef { get; set; } = string.Empty;

    // --- VnPay fields (điền sau khi nhận callback) ---
    public string? VnpayTransactionId { get; set; }
    public string? VnpayResponseCode { get; set; }
    public string? VnpayBankCode { get; set; }
    public string? VnpayCardType { get; set; }
    public DateTime? PaidAtUtc { get; set; }

    /// <summary>Raw query string callback từ VnPay (lưu để audit).</summary>
    public string? VnpayRawResponse { get; set; }

    /// <summary>Thời điểm đơn hàng hết hạn chờ thanh toán (thường 15 phút).</summary>
    public DateTime ExpiredAtUtc { get; set; }

    public User User { get; set; } = null!;
    public TokenPackage Package { get; set; } = null!;
    public StudentTokenWallet? Wallet { get; set; }
}

/// <summary>
/// Ví token của học sinh — tổng token còn lại và đã dùng.
/// Mỗi học sinh có đúng 1 wallet; tổng hợp từ nhiều đơn hàng.
/// </summary>
public class StudentTokenWallet : AuditableEntity
{
    public long UserId { get; set; }

    /// <summary>Tổng token đang còn có thể dùng.</summary>
    public int AvailableTokens { get; set; }

    /// <summary>Tổng token đã dùng từ trước đến nay.</summary>
    public int UsedTokens { get; set; }

    /// <summary>Ngày wallet hết hạn (theo gói gần nhất, null = không hết hạn).</summary>
    public DateTime? ExpiresAtUtc { get; set; }

    public User User { get; set; } = null!;
    public ICollection<TokenTransaction> Transactions { get; set; } = new List<TokenTransaction>();
}

/// <summary>
/// Lịch sử từng lần cộng / trừ token trong ví.
/// </summary>
public class TokenTransaction : CreatedEntity
{
    public long WalletId { get; set; }
    public long UserId { get; set; }

    public TokenTransactionType Type { get; set; }

    /// <summary>Số token thay đổi (dương = cộng, âm = trừ).</summary>
    public int Delta { get; set; }

    /// <summary>Số token còn lại SAU giao dịch này.</summary>
    public int BalanceAfter { get; set; }

    /// <summary>Mô tả ngữ cảnh: "Mua gói Cơ Bản", "Chat message #123", v.v.</summary>
    public string? Description { get; set; }

    /// <summary>Liên kết đến đơn mua (nếu type = Purchase).</summary>
    public long? OrderId { get; set; }

    /// <summary>Liên kết đến chat message (nếu type = ChatUsage).</summary>
    public long? ChatMessageId { get; set; }

    public StudentTokenWallet Wallet { get; set; } = null!;
}
