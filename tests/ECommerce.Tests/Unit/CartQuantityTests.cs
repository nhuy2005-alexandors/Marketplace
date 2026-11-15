using ECommerce.Application.Common;
using ECommerce.Application.DTOs.Cart;
using ECommerce.Application.Services;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using FluentAssertions;

namespace ECommerce.Tests.Unit;

public class CartQuantityTests
{
    [Fact]
    public async Task AddItem_QuantityOverflow_IsRejectedWithoutChangingCart()
    {
        using var db = TestDb.Create();
        var buyer = new User { Email = "overflow-buyer@example.invalid", FullName = "Buyer", PasswordHash = "h" };
        var seller = new User { Email = "overflow-seller@example.invalid", FullName = "Seller", PasswordHash = "h", Role = UserRole.Seller, SellerStatus = SellerStatus.Approved };
        var category = new Category { Name = "Overflow" };
        db.Users.AddRange(buyer, seller);
        db.Categories.Add(category);
        await db.SaveChangesAsync();
        var product = new Product { Name = "P", Sku = "OVERFLOW", Price = 1, Stock = int.MaxValue, SellerId = seller.Id, CategoryId = category.Id };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        var service = new CartService(db);
        (await service.AddItemAsync(buyer.Id, new(product.Id, 1))).Success.Should().BeTrue();

        var result = await service.AddItemAsync(buyer.Id, new AddCartItemRequest(product.Id, int.MaxValue));

        result.Success.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.Validation);
        db.CartItems.Single().Quantity.Should().Be(1);
    }
}
