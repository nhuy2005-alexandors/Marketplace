using ECommerce.Application.Common;
using ECommerce.Application.DTOs.Orders;
using ECommerce.Application.DTOs.Refunds;
using ECommerce.Application.Interfaces;
using ECommerce.Application.Services;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using ECommerce.Infrastructure.Payments;
using ECommerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Tests.Unit;

public class LifecycleSafetyTests
{
    internal static async Task<Order> SeedAsync(AppDbContext db, OrderStatus status = OrderStatus.Paid)
    {
        var key = Guid.NewGuid().ToString("N");
        var buyer = new User { Email = $"buyer-{key}@example.invalid", FullName = "Fixture buyer", PasswordHash = "test", Role = UserRole.Customer };
        var seller = new User { Email = $"seller-{key}@example.invalid", FullName = "Fixture seller", PasswordHash = "test", Role = UserRole.Seller };
        var admin = new User { Email = $"admin-{key}@example.invalid", FullName = "Fixture admin", PasswordHash = "test", Role = UserRole.Admin };
        var category = new Category { Name = $"Fixture-{key}" };
        db.AddRange(buyer, seller, admin, category);
        await db.SaveChangesAsync();
        var product = new Product { Name = "Fixture product", Sku = Guid.NewGuid().ToString("N"), Price = 100, Stock = 5, SellerId = seller.Id, CategoryId = category.Id };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        var order = new Order { UserId = buyer.Id, ShippingAddress = "Synthetic", Status = status };
        order.Items.Add(new OrderItem { ProductId = product.Id, ProductName = product.Name, SellerId = seller.Id, Quantity = 2, UnitPrice = 100 });
        order.Payment = new Payment
        {
            Amount = 200,
            Method = PaymentMethod.EWallet,
            Status = status == OrderStatus.Paid ? PaymentStatus.Completed : PaymentStatus.Pending,
            ProviderKey = "momo",
            ProviderAmount = status == OrderStatus.Paid ? 5_000_000m : null,
            ProviderCurrency = status == OrderStatus.Paid ? "VND" : null
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        return order;
    }

    private sealed class Provider(PaymentVerifyResult result) : IPaymentProvider
    {
        public string Key => "momo";
        public Task<PaymentVerifyResult> VerifyAsync(IReadOnlyDictionary<string,string> data, CancellationToken ct = default) => Task.FromResult(result);
        public Task<PaymentInitResult> CreatePaymentAsync(PaymentContext context, CancellationToken ct = default) => throw new NotSupportedException();
    }

    [Theory]
    [InlineData(PaymentStatus.Pending)]
    [InlineData(PaymentStatus.Completed)]
    [InlineData(PaymentStatus.Refunded)]
    public async Task Unauthenticated_callback_never_mutates_financial_state(PaymentStatus status)
    {
        await using var db = TestDb.Create();
        var order = await SeedAsync(db);
        order.Payment!.Status = status;
        await db.SaveChangesAsync();
        var service = new PaymentService(db, new PaymentProviderFactory([new Provider(new(false, order.Id, "", "Invalid signature"))]));
        Assert.False((await service.ConfirmAsync("momo", new Dictionary<string,string>())).Success);
        db.ChangeTracker.Clear();
        Assert.Equal(status, (await db.Payments.SingleAsync()).Status);
        Assert.Equal(OrderStatus.Paid, (await db.Orders.SingleAsync()).Status);
        Assert.Equal(5, (await db.Products.SingleAsync()).Stock);
    }

    [Fact]
    public async Task Pending_refund_holds_seller_and_admin_until_rejection()
    {
        await using var db = TestDb.Create();
        var order = await SeedAsync(db);
        var refund = await new RefundService(db).RequestAsync(order.UserId, new(order.Id, "Synthetic refund reason"));
        Assert.True(refund.Success);
        var item = order.Items.Single();
        Assert.False((await new SellerOrderService(db).UpdateFulfillmentAsync(item.SellerId, item.Id, new("Shipped"))).Success);
        Assert.False((await new OrderService(db).UpdateStatusAsync(order.Id, new("Shipped"))).Success);
        var adminId = await db.Users.Where(u => u.Role == UserRole.Admin).Select(u => u.Id).SingleAsync();
        Assert.True((await new RefundService(db).RejectAsync(adminId, refund.Value!.Id, new("Synthetic rejection"))).Success);
        Assert.True((await new SellerOrderService(db).UpdateFulfillmentAsync(item.SellerId, item.Id, new("Shipped"))).Success);
        Assert.Equal(PaymentStatus.Completed, order.Payment!.Status);
        Assert.Equal(5, (await db.Products.SingleAsync()).Stock);
    }

    [Fact]
    public async Task Cod_acceptance_is_not_cash_collection()
    {
        await using var db = TestDb.Create();
        var order = await SeedAsync(db, OrderStatus.Pending);
        var service = new PaymentService(db, new PaymentProviderFactory([new CodPaymentProvider()]));
        Assert.True((await service.InitiateAsync(order.UserId, order.Id, new("cod", null))).Success);
        Assert.Equal(PaymentStatus.Pending, order.Payment!.Status);
        Assert.Null(order.Payment.PaidAt);
        Assert.True((await new OrderService(db).CancelAsync(order.UserId, order.Id)).Success);
        Assert.Equal(PaymentStatus.Cancelled, order.Payment.Status);
    }

    [Theory]
    [InlineData(PaymentStatus.Completed)]
    [InlineData(PaymentStatus.Refunded)]
    public async Task Authenticated_late_failure_cannot_regress_capture(PaymentStatus status)
    {
        await using var db = TestDb.Create();
        var order = await SeedAsync(db);
        SetAttempt(order.Payment!);
        order.Payment!.Status = status;
        order.Payment.TransactionId = "123";
        await db.SaveChangesAsync();
        var service = new PaymentService(db, new PaymentProviderFactory([new Provider(new(false, order.Id, "0", "Failed", true, "attempt", 1000, "VND"))]));
        Assert.True((await service.ConfirmAsync("momo", new Dictionary<string,string>())).Success);
        Assert.Equal(status, order.Payment.Status);
        Assert.Equal("123", order.Payment.TransactionId);
    }

    internal static void SetAttempt(Payment payment)
    {
        payment.ProviderKey = "momo";
        payment.ProviderReference = "attempt";
        payment.ProviderAmount = 1000;
        payment.ProviderCurrency = "VND";
    }

    [Fact]
    public async Task Captured_money_after_cancellation_is_recorded_for_reconciliation_without_restock()
    {
        await using var db = TestDb.Create();
        var order = await SeedAsync(db, OrderStatus.Pending);
        SetAttempt(order.Payment!);
        Assert.True((await new OrderService(db).CancelAsync(order.UserId, order.Id)).Success);
        var provider = new Provider(new(true, order.Id, "123", null, true, "attempt", 1000, "VND"));
        var service = new PaymentService(db, new PaymentProviderFactory([provider]));
        Assert.True((await service.ConfirmAsync("momo", new Dictionary<string,string>())).Success);
        var paidAt = order.Payment!.PaidAt;
        Assert.True(order.Payment.RequiresReconciliation);
        Assert.Equal(PaymentStatus.Completed, order.Payment.Status);
        Assert.Equal(OrderStatus.Cancelled, order.Status);
        Assert.True((await service.ConfirmAsync("momo", new Dictionary<string,string>())).Success);
        Assert.Equal(paidAt, order.Payment.PaidAt);
        Assert.Equal(7, (await db.Products.SingleAsync()).Stock);
    }

    [Theory]
    [InlineData("wrong", 1000, "VND")]
    public async Task Signed_but_mismatched_attempt_never_mutates_payment(string reference, int amount, string currency)
    {
        await using var db = TestDb.Create();
        var order = await SeedAsync(db, OrderStatus.Pending);
        SetAttempt(order.Payment!);
        var provider = new Provider(new(true, order.Id, "123", null, true, reference, amount, currency));
        var result = await new PaymentService(db, new PaymentProviderFactory([provider])).ConfirmAsync("momo", new Dictionary<string,string>());
        Assert.False(result.Success);
        Assert.Equal(PaymentStatus.Pending, order.Payment!.Status);
        Assert.Equal(OrderStatus.Pending, order.Status);
    }

    [Fact]
    public async Task Unknown_provider_fails_closed_and_demo_is_disabled_by_default()
    {
        Assert.Throws<ArgumentException>(() => TestDb.PaymentFactory().Resolve("not-a-provider"));
        var init = await new MockPaymentProvider().CreatePaymentAsync(new(1,100,"",""));
        Assert.False(init.Completed);
        Assert.NotNull(init.Error);
    }

    [Fact]
    public async Task Shipped_cod_is_not_collected_revenue_and_cannot_be_cancelled()
    {
        await using var db=TestDb.Create();
        var order=await SeedAsync(db,OrderStatus.Pending);
        var service=new PaymentService(db,new PaymentProviderFactory([new CodPaymentProvider()]));
        Assert.True((await service.InitiateAsync(order.UserId,order.Id,new("cod",null))).Success);
        var item=order.Items.Single();
        Assert.True((await new SellerOrderService(db).UpdateFulfillmentAsync(item.SellerId,item.Id,new("Shipped"))).Success);
        Assert.Equal(0,(await new DashboardService(db).GetAsync(null)).TotalRevenue);
        Assert.False((await new OrderService(db).CancelAsync(order.UserId,order.Id)).Success);
        Assert.Equal(PaymentStatus.Pending,order.Payment!.Status);
    }

    [Fact]
    public async Task Demo_capture_is_never_eligible_for_real_refund()
    {
        await using var db=TestDb.Create();
        var order=await SeedAsync(db);
        order.Payment!.IsDemo=true;
        await db.SaveChangesAsync();
        Assert.False((await new RefundService(db).RequestAsync(order.UserId,new(order.Id,"Synthetic refund request"))).Success);
        Assert.Empty(db.Refunds);
        Assert.False(order.RefundPending);
        Assert.Equal(0,(await new DashboardService(db).GetAsync(null)).TotalRevenue);
    }

    [Fact]
    public async Task Refund_history_filter_keeps_ownership_and_finds_requested_order()
    {
        await using var db=TestDb.Create();
        var mine=await SeedAsync(db);
        var other=await SeedAsync(db);
        var service=new RefundService(db);
        Assert.True((await service.RequestAsync(mine.UserId,new(mine.Id,"Owned request reason"))).Success);
        Assert.True((await service.RequestAsync(other.UserId,new(other.Id,"Other request reason"))).Success);
        var filtered=await service.GetForUserAsync(mine.UserId,1,100,orderIds:[mine.Id,other.Id]);
        Assert.Single(filtered.Value!.Items);
        Assert.Equal(mine.Id,filtered.Value.Items[0].OrderId);
        Assert.Empty((await service.GetForUserAsync(mine.UserId,1,100,orderIds:[other.Id])).Value!.Items);
    }

    [Fact]
    public async Task Legacy_attempt_capture_is_recorded_for_reconciliation_without_inventing_historical_quote()
    {
        await using var db=TestDb.Create();
        var order=await SeedAsync(db,OrderStatus.Pending);
        order.Payment!.TransactionId="legacy-request";
        await db.SaveChangesAsync();
        var callback=new Provider(new(true,order.Id,"777",null,true,"legacy-request",12345,"VND"));
        Assert.True((await new PaymentService(db,new PaymentProviderFactory([callback])).ConfirmAsync("momo",new Dictionary<string,string>())).Success);
        Assert.Equal(PaymentStatus.Completed,order.Payment.Status);
        Assert.True(order.Payment.RequiresReconciliation);
        Assert.Equal(12345,order.Payment.ProviderAmount);
        Assert.Equal(OrderStatus.Pending,order.Status);
        Assert.Equal(5,(await db.Products.SingleAsync()).Stock);
        Assert.False((await new SellerOrderService(db).UpdateFulfillmentAsync(order.Items.Single().SellerId,order.Items.Single().Id,new("Shipped"))).Success);
    }

    [Theory]
    [InlineData(PaymentStatus.Pending)]
    [InlineData(PaymentStatus.Completed)]
    [InlineData(PaymentStatus.Refunded)]
    public async Task Signed_inconsistent_capture_is_durable_without_activating_or_regressing_order(PaymentStatus status)
    {
        await using var db=TestDb.Create();
        var order=await SeedAsync(db,OrderStatus.Pending);
        SetAttempt(order.Payment!);
        order.Payment!.Status=status;
        if(status!=PaymentStatus.Pending)order.Payment.TransactionId="original-capture";
        await db.SaveChangesAsync();
        var callback=new Provider(new(true,order.Id,"new-capture",null,true,"attempt",2000,"VND"));
        var service=new PaymentService(db,new PaymentProviderFactory([callback]));
        Assert.True((await service.ConfirmAsync("momo",new Dictionary<string,string>())).Success);
        Assert.True(order.Payment.RequiresReconciliation);
        Assert.Equal(1000,order.Payment.ProviderAmount);
        Assert.Equal(2000,order.Payment.ReconciliationAmount);
        Assert.Equal("new-capture",order.Payment.ReconciliationReference);
        Assert.Equal(status==PaymentStatus.Pending?PaymentStatus.Completed:status,order.Payment.Status);
        if(status!=PaymentStatus.Pending)Assert.Equal("original-capture",order.Payment.TransactionId);
        else Assert.Equal("new-capture",order.Payment.TransactionId);
        Assert.Equal(OrderStatus.Pending,order.Status);
        Assert.Equal(5,(await db.Products.SingleAsync()).Stock);
        Assert.True((await service.ConfirmAsync("momo",new Dictionary<string,string>())).Success);
    }
    [Fact]
    public async Task Cancelled_order_items_are_excluded_from_revenue_and_top_products()
    {
        await using var db = TestDb.Create();
        var order = await SeedAsync(db);
        order.Items.Single().ChangeStatus(FulfillmentStatus.Cancelled);
        await db.SaveChangesAsync();

        var dashboard = await new DashboardService(db).GetAsync();

        Assert.Equal(0m, dashboard.TotalRevenue);
        Assert.Empty(dashboard.TopProducts);
    }

}
