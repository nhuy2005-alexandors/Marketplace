using ECommerce.Application.Common;
using ECommerce.Application.DTOs.Cart;
using ECommerce.Application.DTOs.Orders;
using ECommerce.Application.DTOs.Reviews;
using ECommerce.Application.Services;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ECommerce.Tests.Unit;

public class ReviewServiceTests
{
    private static async Task<(int userId, int productId)> SeedAsync(Infrastructure.Persistence.AppDbContext db)
    {
        var user = new User { Email = "u@x.com", PasswordHash = "h", FullName = "Buyer", Role = UserRole.Customer };
        var seller = new User
        {
            Email = "s@x.com", PasswordHash = "h", FullName = "Seller",
            Role = UserRole.Seller, SellerStatus = SellerStatus.Approved
        };
        var category = new Category { Name = "Cat" };
        db.Users.AddRange(user, seller);
        db.Categories.Add(category);
        await db.SaveChangesAsync();
        var product = new Product
        {
            Name = "P", Price = 50m, Stock = 10, CategoryId = category.Id, SellerId = seller.Id
        };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return (user.Id, product.Id);
    }

    private static async Task CompletePurchaseAsync(
        Infrastructure.Persistence.AppDbContext db, int userId, bool isDemo = false)
    {
        var order = await db.Orders.Include(o => o.Payment).SingleAsync(o => o.UserId == userId);
        order.Status = OrderStatus.Paid;
        order.Payment ??= new Payment
        {
            Amount = order.Total,
            Method = PaymentMethod.EWallet
        };
        order.Payment.Status = PaymentStatus.Completed;
        order.Payment.IsDemo = isDemo;
        order.Payment.RequiresReconciliation = false;
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task CreateReview_WithoutPurchase_IsForbidden()
    {
        using var db = TestDb.Create();
        var (userId, productId) = await SeedAsync(db);

        var result = await new ReviewService(db).CreateAsync(userId, productId, new CreateReviewRequest(5, "nice"));

        result.Success.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.Forbidden);
    }

    [Fact]
    public async Task CreateReview_AfterCompletedNonDemoPurchase_Succeeds()
    {
        using var db = TestDb.Create();
        var (userId, productId) = await SeedAsync(db);
        var cart = new CartService(db);
        await cart.AddItemAsync(userId, new AddCartItemRequest(productId, 1));
        await new OrderService(db).CheckoutAsync(userId, new CheckoutRequest("x"));
        await CompletePurchaseAsync(db, userId);

        var result = await new ReviewService(db).CreateAsync(userId, productId, new CreateReviewRequest(4, "good"));

        result.Success.Should().BeTrue();
        result.Value!.Rating.Should().Be(4);
    }

    [Fact]
    public async Task CreateReview_PendingPayment_IsForbidden()
    {
        using var db = TestDb.Create();
        var (userId, productId) = await SeedAsync(db);
        await new CartService(db).AddItemAsync(userId, new AddCartItemRequest(productId, 1));
        await new OrderService(db).CheckoutAsync(userId, new CheckoutRequest("x"));

        var result = await new ReviewService(db).CreateAsync(userId, productId, new CreateReviewRequest(4, "unpaid"));

        result.Success.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.Forbidden);
    }

    [Fact]
    public async Task CreateReview_DemoPurchase_IsForbidden()
    {
        using var db = TestDb.Create();
        var (userId, productId) = await SeedAsync(db);
        await new CartService(db).AddItemAsync(userId, new AddCartItemRequest(productId, 1));
        await new OrderService(db).CheckoutAsync(userId, new CheckoutRequest("x"));
        await CompletePurchaseAsync(db, userId, isDemo: true);

        var result = await new ReviewService(db).CreateAsync(userId, productId, new CreateReviewRequest(4, "demo"));

        result.Success.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.Forbidden);
    }

    [Fact]
    public async Task CreateReview_Duplicate_IsConflict()
    {
        using var db = TestDb.Create();
        var (userId, productId) = await SeedAsync(db);
        var cart = new CartService(db);
        await cart.AddItemAsync(userId, new AddCartItemRequest(productId, 1));
        await new OrderService(db).CheckoutAsync(userId, new CheckoutRequest("x"));
        await CompletePurchaseAsync(db, userId);
        var reviews = new ReviewService(db);
        await reviews.CreateAsync(userId, productId, new CreateReviewRequest(4, "good"));

        var result = await reviews.CreateAsync(userId, productId, new CreateReviewRequest(3, "again"));

        result.Success.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.Conflict);
    }
}
