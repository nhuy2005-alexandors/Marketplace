using ECommerce.Application.Common;
using ECommerce.Application.DTOs.Cart;
using ECommerce.Application.DTOs.Orders;
using ECommerce.Application.Interfaces;
using ECommerce.Application.Services;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace ECommerce.Tests.Unit;

public class CheckoutFlowTests
{
    private static async Task<(int userId, int productId)> SeedAsync(Infrastructure.Persistence.AppDbContext db, int stock = 10)
    {
        var user = new User { Email = "u@x.com", PasswordHash = "h", FullName = "U", Role = UserRole.Customer };
        var category = new Category { Name = "Cat" };
        db.Users.Add(user);
        db.Categories.Add(category);
        await db.SaveChangesAsync();
        var product = new Product { Name = "P", Price = 50m, Stock = stock, CategoryId = category.Id };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return (user.Id, product.Id);
    }

    [Fact]
    public async Task Checkout_MovesCartToOrder_AndDecreasesStock()
    {
        using var db = TestDb.Create();
        var (userId, productId) = await SeedAsync(db, stock: 10);

        var cart = new CartService(db);
        await cart.AddItemAsync(userId, new AddCartItemRequest(productId, 3));

        var orders = new OrderService(db);
        var result = await orders.CheckoutAsync(userId, new CheckoutRequest("123 St"));

        result.Success.Should().BeTrue();
        result.Value!.Total.Should().Be(150m);
        result.Value.Status.Should().Be("Pending");

        var product = await db.Products.FindAsync(productId);
        product!.Stock.Should().Be(7);

        (await cart.GetAsync(userId)).Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Checkout_EmptyCart_Fails()
    {
        using var db = TestDb.Create();
        var (userId, _) = await SeedAsync(db);
        var orders = new OrderService(db);

        var result = await orders.CheckoutAsync(userId, new CheckoutRequest("123 St"));

        result.Success.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task AddToCart_ExceedingStock_Fails()
    {
        using var db = TestDb.Create();
        var (userId, productId) = await SeedAsync(db, stock: 2);
        var cart = new CartService(db);

        var result = await cart.AddItemAsync(userId, new AddCartItemRequest(productId, 5));

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task PayOrder_CompletesPayment_AndMarksPaid()
    {
        using var db = TestDb.Create();
        var (userId, productId) = await SeedAsync(db);
        var cart = new CartService(db);
        await cart.AddItemAsync(userId, new AddCartItemRequest(productId, 1));
        var orders = new OrderService(db);
        var order = (await orders.CheckoutAsync(userId, new CheckoutRequest("x"))).Value!;

        var payments = new PaymentService(db, TestDb.PaymentFactory());
        var result = await payments.InitiateAsync(userId, order.Id, new PayOrderRequest("mock", null));

        result.Success.Should().BeTrue();
        result.Value!.RequiresRedirect.Should().BeFalse();
        result.Value.Order!.Status.Should().Be("Paid");
        result.Value.Order.Payment!.Status.Should().Be("Completed");
    }

    [Fact]
    public async Task CancelOrder_RestoresStock()
    {
        using var db = TestDb.Create();
        var (userId, productId) = await SeedAsync(db, stock: 10);
        var cart = new CartService(db);
        await cart.AddItemAsync(userId, new AddCartItemRequest(productId, 4));
        var orders = new OrderService(db);
        var order = (await orders.CheckoutAsync(userId, new CheckoutRequest("x"))).Value!;

        (await db.Products.FindAsync(productId))!.Stock.Should().Be(6);

        var result = await orders.CancelAsync(userId, order.Id);

        result.Success.Should().BeTrue();
        result.Value!.Status.Should().Be("Cancelled");
        (await db.Products.FindAsync(productId))!.Stock.Should().Be(10);
    }

    [Fact]
    public async Task Checkout_WithPercentageCoupon_AppliesDiscount()
    {
        using var db = TestDb.Create();
        var (userId, productId) = await SeedAsync(db, stock: 10);
        db.Coupons.Add(new Domain.Entities.Coupon
        {
            Code = "SAVE10",
            Type = Domain.Entities.DiscountType.Percentage,
            Value = 10,
            MinOrderAmount = 0,
            IsActive = true
        });
        await db.SaveChangesAsync();

        var cart = new CartService(db);
        await cart.AddItemAsync(userId, new AddCartItemRequest(productId, 2)); // 100
        var orders = new OrderService(db);

        var result = await orders.CheckoutAsync(userId, new CheckoutRequest("addr", "SAVE10"));

        result.Success.Should().BeTrue();
        result.Value!.Subtotal.Should().Be(100m);
        result.Value.DiscountAmount.Should().Be(10m);
        result.Value.Total.Should().Be(90m);
        result.Value.CouponCode.Should().Be("SAVE10");
    }

    [Fact]
    public async Task Checkout_WithInvalidCoupon_Fails()
    {
        using var db = TestDb.Create();
        var (userId, productId) = await SeedAsync(db, stock: 10);
        var cart = new CartService(db);
        await cart.AddItemAsync(userId, new AddCartItemRequest(productId, 1));
        var orders = new OrderService(db);

        var result = await orders.CheckoutAsync(userId, new CheckoutRequest("addr", "NOPE"));

        result.Success.Should().BeFalse();
    }
}
