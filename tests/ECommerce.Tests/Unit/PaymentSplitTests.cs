using ECommerce.Application.Common;
using ECommerce.Application.Services;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace ECommerce.Tests.Unit;

public class PaymentSplitTests
{
    [Fact]
    public async Task SplitAndDashboard_LargeValidAmounts_DoNotOverflowProration()
    {
        using var db = TestDb.Create();
        var (buyerId, sellerA, _, orderId) = await SeedOrderAsync(db, 1_000_000_000_000_000m);
        var order = db.Orders.Single(o => o.Id == orderId);
        foreach (var item in order.Items) { item.UnitPrice = 1_000_000_000_000_000m; item.Quantity = 1; }
        order.Status = OrderStatus.Paid;
        order.Payment = new Payment { Amount = order.Total, Status = PaymentStatus.Completed, Method = PaymentMethod.EWallet };
        await db.SaveChangesAsync();

        var split = (await new OrderService(db).GetSplitAsync(buyerId, false, orderId)).Value!;

        split.Sellers.Sum(s => s.DiscountShare).Should().Be(order.DiscountAmount);
        (await new DashboardService(db).GetAsync(sellerA)).TotalRevenue.Should().Be(500_000_000_000_000m);
    }

    [Fact]
    public async Task SellerOrderPayment_DoesNotExposePlatformPaymentIdentifiersOrAmount()
    {
        using var db = TestDb.Create();
        var (_, sellerA, _, orderId) = await SeedOrderAsync(db, 10m);
        var order = db.Orders.Single(o => o.Id == orderId);
        order.Payment = new Payment
        {
            Amount = order.Total, Method = PaymentMethod.EWallet, Status = PaymentStatus.Completed,
            TransactionId = "platform-provider-transaction", RequiresReconciliation = true,
            ReconciliationAmount = 123456m, ReconciliationCurrency = "VND", ReconciliationReference = "platform-reconciliation"
        };
        await db.SaveChangesAsync();

        var dto = (await new SellerOrderService(db).GetForSellerAsync(sellerA, 1, 20)).Items.Single();

        dto.Payment!.Amount.Should().Be(dto.Total);
        dto.Payment.TransactionId.Should().BeNull();
        dto.Payment.ReconciliationAmount.Should().BeNull();
        dto.Payment.ReconciliationCurrency.Should().BeNull();
        dto.Payment.ReconciliationReference.Should().BeNull();
        dto.Payment.Status.Should().Be("Completed");
        dto.Payment.RequiresReconciliation.Should().BeTrue();
    }

    [Fact]
    public async Task SellerOrdersAndDashboards_UseTheSameCentAllocationAsOrderSplit()
    {
        using var db = TestDb.Create();
        var buyer = new User { Email = "allocation-buyer@example.invalid", FullName = "Buyer", PasswordHash = "h" };
        var sellers = Enumerable.Range(1, 3).Select(i => new User
        {
            Email = $"allocation-seller{i}@example.invalid", FullName = $"Seller {i}", PasswordHash = "h", Role = UserRole.Seller
        }).ToArray();
        db.Users.Add(buyer);
        db.Users.AddRange(sellers);
        await db.SaveChangesAsync();
        var order = new Order { UserId = buyer.Id, ShippingAddress = "Audit", Status = OrderStatus.Paid, DiscountAmount = 1m };
        foreach (var seller in sellers)
            order.Items.Add(new OrderItem { ProductId = seller.Id, SellerId = seller.Id, ProductName = "P", UnitPrice = 1m, Quantity = 1 });
        order.Payment = new Payment { Amount = 2m, Status = PaymentStatus.Completed, Method = PaymentMethod.EWallet };
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        var split = (await new OrderService(db).GetSplitAsync(buyer.Id, false, order.Id)).Value!;

        foreach (var seller in sellers)
        {
            var expected = split.Sellers.Single(s => s.SellerId == seller.Id);
            var sellerOrder = (await new SellerOrderService(db).GetForSellerAsync(seller.Id, 1, 20)).Items.Single();
            sellerOrder.DiscountAmount.Should().Be(expected.DiscountShare);
            sellerOrder.Total.Should().Be(expected.NetTotal);
            (await new DashboardService(db).GetAsync(seller.Id)).TotalRevenue.Should().Be(expected.NetTotal);
        }
        (await new DashboardService(db).GetAsync()).TotalRevenue.Should().Be(split.Total);
    }

    [Fact]
    public async Task Split_SmallSellerSubtotals_DoNotLoseTheDiscountRemainder()
    {
        using var db = TestDb.Create();
        var (_, _, _, orderId) = await SeedOrderAsync(db, 0.02m);
        var order = db.Orders.Single(o => o.Id == orderId);
        foreach (var item in order.Items) { item.UnitPrice = 0.01m; item.Quantity = 1; }
        order.Items.Add(new OrderItem { SellerId = 100, ProductId = 100, ProductName = "C", UnitPrice = 0.01m, Quantity = 1 });
        order.Items.Add(new OrderItem { SellerId = 101, ProductId = 101, ProductName = "D", UnitPrice = 0.01m, Quantity = 1 });
        await db.SaveChangesAsync();

        var split = (await new OrderService(db).GetSplitAsync(0, true, orderId)).Value!;

        split.Sellers.Sum(s => s.DiscountShare).Should().Be(0.02m);
        split.Sellers.Sum(s => s.NetTotal).Should().Be(split.Total);
        split.Sellers.Should().OnlyContain(s => s.DiscountShare >= 0 && s.DiscountShare <= s.Subtotal);
    }

    private static async Task<(int userId, int sellerA, int sellerB, int orderId)> SeedOrderAsync(
        Infrastructure.Persistence.AppDbContext db, decimal discount)
    {
        var buyer = new User { Email = "buyer@s.com", PasswordHash = "h", FullName = "Buyer", Role = UserRole.Customer };
        var a = new User { Email = "a@s.com", PasswordHash = "h", FullName = "A", ShopName = "ShopA", Role = UserRole.Seller };
        var b = new User { Email = "b@s.com", PasswordHash = "h", FullName = "B", Role = UserRole.Seller };
        db.Users.AddRange(buyer, a, b);
        await db.SaveChangesAsync();

        var order = new Order
        {
            UserId = buyer.Id,
            Status = OrderStatus.Pending,
            ShippingAddress = "addr",
            DiscountAmount = discount
        };
        // SellerA: 100 (2x50). SellerB: 33.33 (1x33.33) -> uneven for rounding test.
        order.Items.Add(new OrderItem { ProductId = 1, SellerId = a.Id, ProductName = "PA", UnitPrice = 50m, Quantity = 2 });
        order.Items.Add(new OrderItem { ProductId = 2, SellerId = b.Id, ProductName = "PB", UnitPrice = 33.33m, Quantity = 1 });
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        return (buyer.Id, a.Id, b.Id, order.Id);
    }

    [Fact]
    public async Task Split_GroupsBySeller_WithShopNameFallback()
    {
        using var db = TestDb.Create();
        var (userId, sellerA, sellerB, orderId) = await SeedOrderAsync(db, discount: 0m);
        var svc = new OrderService(db);

        var result = await svc.GetSplitAsync(userId, isAdmin: false, orderId);

        result.Success.Should().BeTrue();
        var split = result.Value!;
        split.Sellers.Should().HaveCount(2);
        split.Sellers.Single(s => s.SellerId == sellerA).ShopName.Should().Be("ShopA");
        // Seller B has no ShopName -> falls back to FullName.
        split.Sellers.Single(s => s.SellerId == sellerB).ShopName.Should().Be("B");
        split.Sellers.Single(s => s.SellerId == sellerA).Subtotal.Should().Be(100m);
        split.Sellers.Single(s => s.SellerId == sellerB).Subtotal.Should().Be(33.33m);
    }

    [Fact]
    public async Task Split_DiscountShares_SumToOrderDiscount_ExactlyDespiteRounding()
    {
        using var db = TestDb.Create();
        // 10 discount across 100 / 33.33 subtotals -> non-terminating proration, forces remainder handling.
        var (userId, _, _, orderId) = await SeedOrderAsync(db, discount: 10m);
        var svc = new OrderService(db);

        var split = (await svc.GetSplitAsync(userId, isAdmin: false, orderId)).Value!;

        split.Sellers.Sum(s => s.DiscountShare).Should().Be(10m);
        split.Sellers.Sum(s => s.NetTotal).Should().Be(split.Total);
        split.Sellers.Should().OnlyContain(s => s.NetTotal == s.Subtotal - s.DiscountShare);
    }

    [Fact]
    public async Task Split_OtherUser_Forbidden()
    {
        using var db = TestDb.Create();
        var (_, _, _, orderId) = await SeedOrderAsync(db, discount: 0m);
        var svc = new OrderService(db);

        var result = await svc.GetSplitAsync(userId: 9999, isAdmin: false, orderId);

        result.Success.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.Forbidden);
    }

    [Fact]
    public async Task Split_Admin_CanViewAnyOrder()
    {
        using var db = TestDb.Create();
        var (_, _, _, orderId) = await SeedOrderAsync(db, discount: 0m);
        var svc = new OrderService(db);

        var result = await svc.GetSplitAsync(userId: 9999, isAdmin: true, orderId);

        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task Split_MissingOrder_NotFound()
    {
        using var db = TestDb.Create();
        var svc = new OrderService(db);

        var result = await svc.GetSplitAsync(userId: 1, isAdmin: true, orderId: 12345);

        result.Success.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.NotFound);
    }
}
