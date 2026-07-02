using ECommerce.Application.Common;
using ECommerce.Application.DTOs.Cart;
using ECommerce.Application.DTOs.Orders;
using ECommerce.Application.Services;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using ECommerce.Domain.Exceptions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ECommerce.Tests.Unit;

public class FulfillmentTests
{
    [Theory]
    [InlineData(FulfillmentStatus.Pending, FulfillmentStatus.Shipped)]
    [InlineData(FulfillmentStatus.Pending, FulfillmentStatus.Cancelled)]
    [InlineData(FulfillmentStatus.Shipped, FulfillmentStatus.Delivered)]
    public void ItemChangeStatus_AllowsValid(FulfillmentStatus from, FulfillmentStatus to)
    {
        var item = new OrderItem { ProductName = "x", UnitPrice = 1, Quantity = 1, Status = from };
        item.ChangeStatus(to);
        item.Status.Should().Be(to);
    }

    [Theory]
    [InlineData(FulfillmentStatus.Pending, FulfillmentStatus.Delivered)]
    [InlineData(FulfillmentStatus.Delivered, FulfillmentStatus.Shipped)]
    [InlineData(FulfillmentStatus.Cancelled, FulfillmentStatus.Shipped)]
    [InlineData(FulfillmentStatus.Shipped, FulfillmentStatus.Pending)]
    public void ItemChangeStatus_RejectsInvalid(FulfillmentStatus from, FulfillmentStatus to)
    {
        var item = new OrderItem { ProductName = "x", UnitPrice = 1, Quantity = 1, Status = from };
        var act = () => item.ChangeStatus(to);
        act.Should().Throw<InvalidOrderTransitionException>();
    }

    private static async Task<(int sellerA, int sellerB, int prodA, int prodB, int customer)> SeedAsync(Infrastructure.Persistence.AppDbContext db)
    {
        var sa = new User { Email = "sa@x.com", PasswordHash = "h", FullName = "SA", ShopName = "A", Role = UserRole.Seller };
        var sb = new User { Email = "sb@x.com", PasswordHash = "h", FullName = "SB", ShopName = "B", Role = UserRole.Seller };
        var cust = new User { Email = "c@x.com", PasswordHash = "h", FullName = "C", Role = UserRole.Customer };
        var cat = new Category { Name = "Cat" };
        db.Users.AddRange(sa, sb, cust);
        db.Categories.Add(cat);
        await db.SaveChangesAsync();
        var pa = new Product { Name = "PA", Price = 60m, Stock = 10, CategoryId = cat.Id, SellerId = sa.Id };
        var pb = new Product { Name = "PB", Price = 40m, Stock = 10, CategoryId = cat.Id, SellerId = sb.Id };
        db.Products.AddRange(pa, pb);
        await db.SaveChangesAsync();
        return (sa.Id, sb.Id, pa.Id, pb.Id, cust.Id);
    }

    [Fact]
    public async Task Seller_CanFulfillOwnItem_NotOthers()
    {
        using var db = TestDb.Create();
        var (sa, sb, pa, pb, cust) = await SeedAsync(db);
        var cart = new CartService(db);
        await cart.AddItemAsync(cust, new AddCartItemRequest(pa, 1));
        await cart.AddItemAsync(cust, new AddCartItemRequest(pb, 1));
        var order = (await new OrderService(db).CheckoutAsync(cust, new CheckoutRequest("addr"))).Value!;
        var itemA = await db.OrderItems.FirstAsync(i => i.SellerId == sa);

        var seller = new SellerOrderService(db);
        // sellerB cố ship item của sellerA -> Forbidden
        var forbidden = await seller.UpdateFulfillmentAsync(sb, itemA.Id, new UpdateFulfillmentStatusRequest("Shipped"));
        forbidden.Success.Should().BeFalse();
        forbidden.ErrorType.Should().Be(ErrorType.Forbidden);

        // sellerA ship item của mình -> OK
        var ok = await seller.UpdateFulfillmentAsync(sa, itemA.Id, new UpdateFulfillmentStatusRequest("Shipped"));
        ok.Success.Should().BeTrue();
        ok.Value!.Status.Should().Be("Shipped");
    }

    [Fact]
    public async Task CouponDiscount_IsProRatedPerSeller()
    {
        using var db = TestDb.Create();
        var (sa, sb, pa, pb, cust) = await SeedAsync(db);
        db.Coupons.Add(new Coupon { Code = "SAVE20", Type = DiscountType.FixedAmount, Value = 20, MinOrderAmount = 0, IsActive = true });
        await db.SaveChangesAsync();

        var cart = new CartService(db);
        await cart.AddItemAsync(cust, new AddCartItemRequest(pa, 1)); // 60 (seller A)
        await cart.AddItemAsync(cust, new AddCartItemRequest(pb, 1)); // 40 (seller B)
        // subtotal 100, coupon -20 -> total 80
        await new OrderService(db).CheckoutAsync(cust, new CheckoutRequest("addr", "SAVE20"));

        var seller = new SellerOrderService(db);
        var aOrders = await seller.GetForSellerAsync(sa, 1, 10);
        var bOrders = await seller.GetForSellerAsync(sb, 1, 10);

        // A: 60/100 * 20 = 12 giảm -> total 48; B: 40/100*20=8 -> total 32; tổng 80 = Order.Total
        aOrders.Items[0].DiscountAmount.Should().Be(12m);
        aOrders.Items[0].Total.Should().Be(48m);
        bOrders.Items[0].DiscountAmount.Should().Be(8m);
        bOrders.Items[0].Total.Should().Be(32m);
        (aOrders.Items[0].Total + bOrders.Items[0].Total).Should().Be(80m);
    }

    [Fact]
    public async Task Dashboard_Revenue_IsAfterCouponDiscount()
    {
        using var db = TestDb.Create();
        var (sa, sb, pa, pb, cust) = await SeedAsync(db);
        db.Coupons.Add(new Coupon { Code = "SAVE20", Type = DiscountType.FixedAmount, Value = 20, MinOrderAmount = 0, IsActive = true });
        await db.SaveChangesAsync();
        var cart = new CartService(db);
        await cart.AddItemAsync(cust, new AddCartItemRequest(pa, 1));
        await cart.AddItemAsync(cust, new AddCartItemRequest(pb, 1));
        var order = (await new OrderService(db).CheckoutAsync(cust, new CheckoutRequest("addr", "SAVE20"))).Value!;
        await new PaymentService(db, TestDb.PaymentFactory()).InitiateAsync(cust, order.Id, new PayOrderRequest("mock", null));

        var dash = new DashboardService(db);
        var system = await dash.GetAsync(null);
        var sellerA = await dash.GetAsync(sa);

        system.TotalRevenue.Should().Be(80m);   // sau giảm giá, khớp Order.Total
        sellerA.TotalRevenue.Should().Be(48m);   // phần seller A sau pro-rate
    }
}
