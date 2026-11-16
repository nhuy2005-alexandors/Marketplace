using ECommerce.Application.DTOs.Orders;
using ECommerce.Application.Interfaces;
using ECommerce.Application.Services;
using ECommerce.Domain.Enums;
using ECommerce.Infrastructure.Payments;
using Microsoft.Extensions.Options;

namespace ECommerce.Tests.Unit;

public class VnpayLifecycleTests
{
    [Theory]
    [InlineData("02")]
    [InlineData("09")]
    public async Task Success_after_a_final_failure_requires_reconciliation(string status)
    {
        await using var db = TestDb.Create();
        var order = await LifecycleSafetyTests.SeedAsync(db, OrderStatus.Pending);
        order.Payment!.ProviderKey = "vnpay";
        order.Payment.ProviderReference = $"{order.Id}x20261115120000x0123456789abcdef0123456789abcdef";
        order.Payment.ProviderAmount = 25637; order.Payment.ProviderCurrency = "VND";
        await db.SaveChangesAsync();
        var service = new PaymentService(db, new PaymentProviderFactory([new VnpayProvider(Options.Create(VnpayProviderTests.Settings()))]));
        var data = VnpayProviderTests.Callback(order.Payment.ProviderReference);
        data["vnp_ResponseCode"] = "24"; data["vnp_TransactionStatus"] = status; data["vnp_TransactionNo"] = "0"; VnpayProviderTests.Sign(data);
        Assert.True((await service.ConfirmAsync("vnpay", data)).Success);
        Assert.Equal(PaymentStatus.Failed, order.Payment.Status);
        data = VnpayProviderTests.Callback(order.Payment.ProviderReference); VnpayProviderTests.Sign(data);
        Assert.True((await service.ConfirmAsync("vnpay", data)).Success);
        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.True(order.Payment.RequiresReconciliation);
        Assert.Equal(25637m, order.Payment.ReconciliationAmount);
    }

    [Fact]
    public async Task Reused_provider_transaction_is_recorded_without_reassigning_it()
    {
        await using var db = TestDb.Create();
        var previous = await LifecycleSafetyTests.SeedAsync(db);
        previous.Payment!.ProviderKey = "vnpay"; previous.Payment.TransactionId = "12345";
        var order = await LifecycleSafetyTests.SeedAsync(db, OrderStatus.Pending);
        order.Payment!.ProviderKey = "vnpay";
        order.Payment.ProviderReference = $"{order.Id}x20261115120000x0123456789abcdef0123456789abcdef";
        order.Payment.ProviderAmount = 25637; order.Payment.ProviderCurrency = "VND";
        await db.SaveChangesAsync();
        var service = new PaymentService(db, new PaymentProviderFactory([new VnpayProvider(Options.Create(VnpayProviderTests.Settings()))]));
        var data = VnpayProviderTests.Callback(order.Payment.ProviderReference); VnpayProviderTests.Sign(data);
        Assert.True((await service.ConfirmAsync("vnpay", data)).Success);
        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.True(order.Payment.RequiresReconciliation);
        Assert.Null(order.Payment.TransactionId);
        Assert.Equal("12345", order.Payment.ReconciliationReference);
    }

    private sealed class QueryProvider : IPaymentProvider, IPaymentQueryProvider
    {
        public string Key => "vnpay";
        public int Calls { get; private set; }
        public PaymentVerifyResult Result { get; set; } = new(false, 0, "", "Unavailable.");
        public Task<PaymentVerifyResult> QueryAsync(string reference, CancellationToken ct = default)
        { Calls++; return Task.FromResult(Result); }
        public Task<PaymentInitResult> CreatePaymentAsync(PaymentContext context, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<PaymentVerifyResult> VerifyAsync(IReadOnlyDictionary<string, string> data, CancellationToken ct = default) => throw new NotSupportedException();
    }

    [Fact]
    public async Task Query_checks_ownership_and_uses_the_same_idempotent_capture_transition()
    {
        await using var db = TestDb.Create();
        var order = await LifecycleSafetyTests.SeedAsync(db, OrderStatus.Pending);
        order.Payment!.ProviderKey = "vnpay";
        order.Payment.ProviderReference = $"{order.Id}x20261115120000x0123456789abcdef0123456789abcdef";
        order.Payment.ProviderAmount = 25637; order.Payment.ProviderCurrency = "VND";
        await db.SaveChangesAsync();
        var provider = new QueryProvider();
        var service = new PaymentService(db, new PaymentProviderFactory([provider]));
        Assert.False((await service.QueryAsync(order.UserId + 100, false, order.Id)).Success);
        Assert.Equal(0, provider.Calls);
        provider.Result = new(false, order.Id, "0", "Still pending", true, order.Payment.ProviderReference, 25637, "VND", false);
        Assert.False((await service.QueryAsync(order.UserId, false, order.Id)).Success);
        Assert.Equal(PaymentStatus.Pending, order.Payment.Status);
        provider.Result = new(true, order.Id, "12345", null, true, order.Payment.ProviderReference, 25637, "VND");
        Assert.True((await service.QueryAsync(order.UserId, false, order.Id)).Success);
        var paidAt = order.Payment.PaidAt;
        Assert.True((await service.QueryAsync(order.UserId + 100, true, order.Id)).Success);
        Assert.Equal(paidAt, order.Payment.PaidAt);
    }

    [Fact]
    public async Task Nonterminal_ipn_never_marks_a_payment_failed()
    {
        await using var db = TestDb.Create();
        var order = await LifecycleSafetyTests.SeedAsync(db, OrderStatus.Pending);
        order.Payment!.ProviderKey = "vnpay";
        order.Payment.ProviderReference = $"{order.Id}x20261115120000x0123456789abcdef0123456789abcdef";
        order.Payment.ProviderAmount = 25637; order.Payment.ProviderCurrency = "VND";
        await db.SaveChangesAsync();
        var data = VnpayProviderTests.Callback(order.Payment.ProviderReference);
        data["vnp_TransactionStatus"] = "01"; data["vnp_TransactionNo"] = "0"; VnpayProviderTests.Sign(data);
        var service = new PaymentService(db, new PaymentProviderFactory([new VnpayProvider(Options.Create(VnpayProviderTests.Settings()))]));
        Assert.False((await service.ConfirmAsync("vnpay", data)).Success);
        Assert.Equal(PaymentStatus.Pending, order.Payment.Status);
    }

    [Fact]
    public async Task Signed_ipn_is_idempotent_and_binds_the_reserved_provider()
    {
        await using var db = TestDb.Create();
        var order = await LifecycleSafetyTests.SeedAsync(db, OrderStatus.Pending);
        var payment = order.Payment!;
        payment.ProviderKey = "vnpay"; payment.ProviderReference = $"{order.Id}x20261115120000x0123456789abcdef0123456789abcdef";
        payment.ProviderAmount = 25637m; payment.ProviderCurrency = "VND";
        await db.SaveChangesAsync();
        var service = new PaymentService(db, new PaymentProviderFactory([new VnpayProvider(Options.Create(VnpayProviderTests.Settings()))]));
        var callback = VnpayProviderTests.Callback(payment.ProviderReference); VnpayProviderTests.Sign(callback);
        var first = await service.ConfirmAsync("vnpay", callback);
        Assert.True(first.Success, first.Error); Assert.Equal(OrderStatus.Paid, order.Status);
        var paidAt = payment.PaidAt;
        Assert.True((await service.ConfirmAsync("vnpay", callback)).Success);
        Assert.Equal(paidAt, payment.PaidAt); Assert.False(payment.RequiresReconciliation);
        payment.ProviderKey = "momo"; await db.SaveChangesAsync();
        Assert.False((await service.ConfirmAsync("vnpay", callback)).Success);
    }

    [Fact]
    public async Task Signed_mismatch_and_late_capture_remain_on_reconciliation_hold()
    {
        await using var db = TestDb.Create();
        var order = await LifecycleSafetyTests.SeedAsync(db, OrderStatus.Cancelled);
        order.Payment!.ProviderKey = "vnpay";
        order.Payment.ProviderReference = $"{order.Id}x20261115120000x0123456789abcdef0123456789abcdef";
        order.Payment.ProviderAmount = 25636m; order.Payment.ProviderCurrency = "VND";
        await db.SaveChangesAsync();
        var service = new PaymentService(db, new PaymentProviderFactory([new VnpayProvider(Options.Create(VnpayProviderTests.Settings()))]));
        var data = VnpayProviderTests.Callback(order.Payment.ProviderReference); VnpayProviderTests.Sign(data);
        var result = await service.ConfirmAsync("vnpay", data);
        Assert.True(result.Success, result.Error);
        Assert.Equal(OrderStatus.Cancelled, order.Status); Assert.True(order.Payment.RequiresReconciliation);
        Assert.Equal(25636m, order.Payment.ProviderAmount); Assert.Equal(25637m, order.Payment.ReconciliationAmount);
    }

    [Fact]
    public async Task Initiation_uses_the_existing_lifecycle_and_frozen_vnd_quote()
    {
        await using var db = TestDb.Create();
        var order = await LifecycleSafetyTests.SeedAsync(db, OrderStatus.Pending);
        var service = new PaymentService(db, new PaymentProviderFactory([new VnpayProvider(Options.Create(VnpayProviderTests.Settings()))]));
        var result = await service.InitiateAsync(order.UserId, order.Id, new PayOrderRequest("vnpay", "https://attacker.invalid"));
        Assert.True(result.Success, result.Error);
        Assert.True(result.Value!.RequiresRedirect); Assert.Equal(PaymentMethod.EWallet, order.Payment!.Method);
        Assert.Equal(5_127_400m, order.Payment.ProviderAmount); Assert.Equal(200m, order.Payment.Amount);
        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.False((await service.InitiateAsync(order.UserId, order.Id, new PayOrderRequest("vnpay", null))).Success);
    }
}
