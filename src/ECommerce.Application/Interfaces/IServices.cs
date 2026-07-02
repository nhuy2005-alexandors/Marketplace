using ECommerce.Domain.Entities;

namespace ECommerce.Application.Interfaces;

public interface IJwtTokenGenerator
{
    string Generate(User user);
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

// Một provider thanh toán: mock, VNPay, Stripe...
public interface IPaymentProvider
{
    // Khóa định danh provider, khớp với PaymentMethod hoặc config
    string Key { get; }

    // Khởi tạo thanh toán. Provider redirect (VNPay/Stripe) trả RedirectUrl + Pending.
    // Provider tức thời (mock/COD) trả Completed ngay.
    Task<PaymentInitResult> CreatePaymentAsync(PaymentContext context, CancellationToken ct = default);

    // Xác minh callback/return từ cổng. Trả kết quả cuối cùng.
    Task<PaymentVerifyResult> VerifyAsync(IReadOnlyDictionary<string, string> callbackData, CancellationToken ct = default);
}

public record PaymentContext(int OrderId, decimal Amount, string ReturnUrl, string ClientIp);

public record PaymentInitResult(
    bool Completed,
    string? RedirectUrl,
    string TransactionId,
    string? Error);

public record PaymentVerifyResult(
    bool Success,
    int OrderId,
    string TransactionId,
    string? Error);

// Factory phân giải provider theo key
public interface IPaymentProviderFactory
{
    IPaymentProvider Resolve(string methodOrProvider);
}
