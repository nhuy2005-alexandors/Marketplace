using ECommerce.Domain.Entities;
using ECommerce.Application.Common;
using ECommerce.Application.DTOs.Refunds;

namespace ECommerce.Application.Interfaces;

public interface IJwtTokenGenerator
{
    string Generate(User user);
    // Sinh refresh token ngẫu nhiên (opaque) kèm thời điểm hết hạn.
    (string Token, DateTime ExpiresAt) GenerateRefreshToken();
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

public interface ICurrentUser
{
    int? UserId { get; }
    bool IsAuthenticated { get; }
}

public interface IFileStorage
{
    // Lưu file ảnh, trả về URL tương đối (vd /uploads/abc.jpg)
    Task<string> SaveImageAsync(Stream content, string fileName, CancellationToken ct = default);
}

// Gửi email/notification. Mock hiện tại chỉ log; sau này swap sang SMTP thật.
public interface IEmailSender
{
    Task SendAsync(string toEmail, string subject, string body, CancellationToken ct = default);
}

// Một provider thanh toán: mock, COD, MoMo...
public interface IPaymentProvider
{
    // Khóa định danh provider, khớp với PaymentMethod hoặc config
    string Key { get; }
    PaymentQuote Quote(PaymentContext context) => new(context.Amount, "XXX", null);

    // Khởi tạo thanh toán. Provider redirect (MoMo) trả RedirectUrl + Pending.
    // Demo mock can return Completed; COD acceptance is not cash collection.
    Task<PaymentInitResult> CreatePaymentAsync(PaymentContext context, CancellationToken ct = default);

    // Xác minh callback/return từ cổng. Trả kết quả cuối cùng.
    Task<PaymentVerifyResult> VerifyAsync(IReadOnlyDictionary<string, string> callbackData, CancellationToken ct = default);
}

// ReturnUrl is retained for contract compatibility; providers must use server-owned redirect configuration.
public record PaymentContext(int OrderId, decimal Amount, string ReturnUrl, string ClientIp, string? RequestId = null);
public record PaymentQuote(decimal Amount, string Currency, string? Error);

public record PaymentInitResult(
    bool Completed,
    string? RedirectUrl,
    string TransactionId,
    string? Error,
    decimal? ProviderAmount = null,
    string? ProviderCurrency = null);

public record PaymentVerifyResult(
    bool Success,
    int OrderId,
    string TransactionId,
    string? Error,
    bool Authenticated = false,
    string? RequestId = null,
    decimal? Amount = null,
    string? Currency = null);

// Factory phân giải provider theo key
public interface IPaymentProviderFactory
{
    IPaymentProvider Resolve(string methodOrProvider);
}

public interface IRefundService
{
    Task<Result<RefundDto>> RequestAsync(int userId, RequestRefundRequest request, CancellationToken ct = default);
    Task<Result<PagedResult<RefundDto>>> GetForUserAsync(int userId, int page, int pageSize, CancellationToken ct = default, int[]? orderIds = null);
    Task<Result<PagedResult<RefundDto>>> GetAllAsync(string? status, int page, int pageSize, CancellationToken ct = default);
    Task<Result<RefundDto>> CompleteAsync(int adminId, int refundId, CompleteRefundRequest request, CancellationToken ct = default);
    Task<Result<RefundDto>> RejectAsync(int adminId, int refundId, RejectRefundRequest request, CancellationToken ct = default);
}
