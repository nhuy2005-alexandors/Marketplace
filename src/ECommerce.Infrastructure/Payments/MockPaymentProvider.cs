using ECommerce.Application.Interfaces;

namespace ECommerce.Infrastructure.Payments;

// Hoàn tất thanh toán tức thì — dùng cho demo và Cash-on-Delivery.
public class MockPaymentProvider : IPaymentProvider
{
    public string Key => "mock";

    public Task<PaymentInitResult> CreatePaymentAsync(PaymentContext ctx, CancellationToken ct = default)
    {
        if (ctx.Amount <= 0)
            return Task.FromResult(new PaymentInitResult(false, null, "", "Invalid amount."));
        var txn = $"MOCK-{Guid.NewGuid():N}";
        return Task.FromResult(new PaymentInitResult(true, null, txn, null));
    }

    public Task<PaymentVerifyResult> VerifyAsync(IReadOnlyDictionary<string, string> data, CancellationToken ct = default)
    {
        var orderId = data.TryGetValue("orderId", out var v) && int.TryParse(v, out var id) ? id : 0;
        return Task.FromResult(new PaymentVerifyResult(true, orderId, $"MOCK-{Guid.NewGuid():N}", null));
    }
}

public class CodPaymentProvider : IPaymentProvider
{
    public string Key => "cod";

    public Task<PaymentInitResult> CreatePaymentAsync(PaymentContext ctx, CancellationToken ct = default)
    {
        var txn = $"COD-{ctx.OrderId}-{Guid.NewGuid():N}";
        return Task.FromResult(new PaymentInitResult(true, null, txn, null));
    }

    public Task<PaymentVerifyResult> VerifyAsync(IReadOnlyDictionary<string, string> data, CancellationToken ct = default)
        => Task.FromResult(new PaymentVerifyResult(true, 0, "", null));
}
