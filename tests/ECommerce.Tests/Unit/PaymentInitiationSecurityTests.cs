using ECommerce.Application.DTOs.Orders;
using ECommerce.Application.Interfaces;
using ECommerce.Application.Services;
using ECommerce.Domain.Enums;
using ECommerce.Infrastructure.Payments;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Tests.Unit;

public class PaymentInitiationSecurityTests
{
    private sealed class CapturingProvider : IPaymentProvider
    {
        public string Key => "momo";
        public PaymentContext? LastContext { get; private set; }

        public PaymentQuote Quote(PaymentContext context) => new(context.Amount * 25000m, "VND", null);

        public Task<PaymentInitResult> CreatePaymentAsync(PaymentContext context, CancellationToken ct = default)
        {
            LastContext = context;
            var quote = Quote(context);
            return Task.FromResult(new PaymentInitResult(
                false, "https://pay.example/checkout", context.RequestId!, null, quote.Amount, quote.Currency));
        }

        public Task<PaymentVerifyResult> VerifyAsync(
            IReadOnlyDictionary<string, string> callbackData, CancellationToken ct = default) =>
            Task.FromResult(new PaymentVerifyResult(false, 0, "", "Not used."));
    }

    [Fact]
    public async Task Client_supplied_return_url_is_not_forwarded_to_payment_provider()
    {
        await using var db = TestDb.Create();
        var order = await LifecycleSafetyTests.SeedAsync(db, OrderStatus.Pending);
        var provider = new CapturingProvider();
        var service = new PaymentService(db, new PaymentProviderFactory([provider]));

        var result = await service.InitiateAsync(
            order.UserId,
            order.Id,
            new PayOrderRequest("momo", "https://attacker.example/collect"));

        Assert.True(result.Success, result.Error);
        Assert.NotNull(provider.LastContext);
        Assert.Equal(string.Empty, provider.LastContext!.ReturnUrl);

        var payment = await db.Payments.SingleAsync(p => p.OrderId == order.Id);
        Assert.Equal(order.Total, payment.Amount); // canonical USD remains unchanged
        Assert.Equal(order.Total * 25000m, payment.ProviderAmount); // frozen provider quote is VND
        Assert.Equal("VND", payment.ProviderCurrency);
    }
}
