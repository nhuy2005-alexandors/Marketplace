using ECommerce.Application.Interfaces;
using Microsoft.Extensions.Options;

namespace ECommerce.Infrastructure.Payments;

// Hoàn tất thanh toán tức thì — dùng cho demo và Cash-on-Delivery.
public class MockPaymentProvider(IOptions<PaymentOptions>? options = null) : IPaymentProvider
{
    public string Key => "mock";
    public PaymentQuote Quote(PaymentContext context) => options?.Value.AllowDemo == true && context.Amount > 0
        ? new(context.Amount, "XXX", null) : new(0, "XXX", "Demo payment is disabled or amount is invalid.");

    public Task<PaymentInitResult> CreatePaymentAsync(PaymentContext ctx, CancellationToken ct = default)
    {
        if (options?.Value.AllowDemo != true)
            return Task.FromResult(new PaymentInitResult(false, null, "", "Demo payment is disabled."));
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
        return Task.FromResult(new PaymentInitResult(false, null, txn, null));
    }

    public Task<PaymentVerifyResult> VerifyAsync(IReadOnlyDictionary<string, string> data, CancellationToken ct = default)
        => Task.FromResult(new PaymentVerifyResult(true, 0, "", null));
}
