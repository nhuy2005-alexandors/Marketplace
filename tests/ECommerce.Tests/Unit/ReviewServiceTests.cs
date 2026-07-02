using ECommerce.Application.Common;
using ECommerce.Application.DTOs.Cart;
using ECommerce.Application.DTOs.Orders;
using ECommerce.Application.DTOs.Reviews;
using ECommerce.Application.Services;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace ECommerce.Tests.Unit;

public class ReviewServiceTests
{
    private static async Task<(int userId, int productId)> SeedAsync(Infrastructure.Persistence.AppDbContext db)
    {
        var user = new User { Email = "u@x.com", PasswordHash = "h", FullName = "Buyer", Role = UserRole.Customer };
        var category = new Category { Name = "Cat" };
        db.Users.Add(user);
        db.Categories.Add(category);
        await db.SaveChangesAsync();
        var product = new Product { Name = "P", Price = 50m, Stock = 10, CategoryId = category.Id };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return (user.Id, product.Id);
    }

    [Fact]
    public async Task CreateReview_WithoutPurchase_IsForbidden()
    {
        using var db = TestDb.Create();
        var (userId, productId) = await SeedAsync(db);
        var reviews = new ReviewService(db);

        var result = await reviews.CreateAsync(userId, productId, new CreateReviewRequest(5, "nice"));

        result.Success.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.Forbidden);
    }

    [Fact]
    public async Task CreateReview_AfterPurchase_Succeeds()
    {
        using var db = TestDb.Create();
        var (userId, productId) = await SeedAsync(db);
        var cart = new CartService(db);
        await cart.AddItemAsync(userId, new AddCartItemRequest(productId, 1));
        await new OrderService(db).CheckoutAsync(userId, new CheckoutRequest("x"));

        var reviews = new ReviewService(db);
        var result = await reviews.CreateAsync(userId, productId, new CreateReviewRequest(4, "good"));

        result.Success.Should().BeTrue();
        result.Value!.Rating.Should().Be(4);
    }

    [Fact]
    public async Task CreateReview_Duplicate_IsConflict()
    {
        using var db = TestDb.Create();
        var (userId, productId) = await SeedAsync(db);
        var cart = new CartService(db);
        await cart.AddItemAsync(userId, new AddCartItemRequest(productId, 1));
        await new OrderService(db).CheckoutAsync(userId, new CheckoutRequest("x"));
        var reviews = new ReviewService(db);
        await reviews.CreateAsync(userId, productId, new CreateReviewRequest(4, "good"));

        var result = await reviews.CreateAsync(userId, productId, new CreateReviewRequest(3, "again"));

        result.Success.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.Conflict);
    }
}
