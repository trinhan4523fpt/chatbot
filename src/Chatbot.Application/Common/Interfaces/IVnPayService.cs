namespace Chatbot.Application.Common.Interfaces;

/// <summary>
/// Giao diện tích hợp cổng thanh toán VnPay.
/// Implementation thực sẽ được đăng ký vào DI khi bạn thêm file VnPay.
/// </summary>
public interface IVnPayService
{
    /// <summary>
    /// Tạo URL chuyển hướng sang trang thanh toán VnPay.
    /// </summary>
    /// <param name="orderId">ID đơn hàng nội bộ.</param>
    /// <param name="orderRef">Mã đơn hàng gửi lên VnPay (vnp_TxnRef).</param>
    /// <param name="amount">Số tiền VND.</param>
    /// <param name="orderInfo">Thông tin đơn hàng hiển thị cho khách.</param>
    /// <param name="returnUrl">URL callback sau khi thanh toán.</param>
    /// <param name="ipAddress">Địa chỉ IP của khách hàng.</param>
    /// <returns>URL redirect sang VnPay.</returns>
    string CreatePaymentUrl(
        long orderId,
        string orderRef,
        decimal amount,
        string orderInfo,
        string returnUrl,
        string ipAddress);

    /// <summary>
    /// Xác thực chữ ký HMAC-SHA512 từ callback VnPay.
    /// </summary>
    /// <param name="queryParams">Query string params từ VnPay callback.</param>
    /// <returns>Kết quả xác thực và thông tin giao dịch.</returns>
    VnPayCallbackResult ValidateCallback(IDictionary<string, string> queryParams);
}

/// <summary>Kết quả xác thực callback từ VnPay.</summary>
public sealed record VnPayCallbackResult(
    bool IsValidSignature,
    string OrderRef,
    string ResponseCode,
    string TransactionId,
    decimal Amount,
    string BankCode,
    string CardType,
    DateTime PayDate,
    string RawResponse);
