using ECommerce.Application.Common;
using ECommerce.Application.Services;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace ECommerce.Tests.Unit;

public class PaymentSplitTests
{
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
