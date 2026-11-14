using ECommerce.Application.Common;
using ECommerce.Application.DTOs.Cart;
using ECommerce.Application.DTOs.Orders;
using ECommerce.Application.Services;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using ECommerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Tests.Unit;

public class SellerVisibilityCartTests
{
    [Fact]
    public async Task AddItem_rejects_product_owned_by_pending_seller_without_creating_cart()
    {
        await using var db = TestDb.Create();
        var (buyer, _, product, _) = await SeedCartAsync(db, SellerStatus.Pending);

        var result = await new CartService(db).AddItemAsync(buyer.Id, new AddCartItemRequest(product.Id, 1));

        Assert.False(result.Success);
        Assert.Equal(ErrorType.NotFound, result.ErrorType);
        Assert.False(await db.Carts.AnyAsync(c => c.UserId == buyer.Id));
        Assert.Empty(await db.CartItems.ToListAsync());
    }

    [Fact]
    public async Task UpdateItem_rejects_existing_item_after_seller_becomes_unapproved()
    {
        await using var db = TestDb.Create();
        var (buyer, seller, product, cartItem) = await SeedCartAsync(db, SellerStatus.Approved, includeCartItem: true);
        seller.SellerStatus = SellerStatus.Pending;
        await db.SaveChangesAsync();

        var result = await new CartService(db).UpdateItemAsync(
            buyer.Id, cartItem!.Id, new UpdateCartItemRequest(3));

        Assert.False(result.Success);
        Assert.Equal(ErrorType.Conflict, result.ErrorType);
        Assert.Equal(2, await db.CartItems.Where(i => i.Id == cartItem.Id).Select(i => i.Quantity).SingleAsync());
        Assert.Equal(10, await db.Products.Where(p => p.Id == product.Id).Select(p => p.Stock).SingleAsync());
    }

    [Fact]
    public async Task Checkout_rejects_cart_with_unapproved_seller_without_mutating_stock_or_cart()
    {
        await using var db = TestDb.Create();
        var (buyer, approvedProduct, pendingProduct) = await SeedCheckoutCartAsync(db);

        var result = await new OrderService(db).CheckoutAsync(
            buyer.Id, new CheckoutRequest("Synthetic address"));

        Assert.False(result.Success);
        Assert.Equal(ErrorType.Conflict, result.ErrorType);
        Assert.Equal(10, await db.Products.Where(p => p.Id == approvedProduct.Id).Select(p => p.Stock).SingleAsync());
        Assert.Equal(10, await db.Products.Where(p => p.Id == pendingProduct.Id).Select(p => p.Stock).SingleAsync());
        Assert.Equal(2, await db.CartItems.CountAsync());
        Assert.Empty(await db.Orders.ToListAsync());
    }

    private static async Task<(User Buyer, User Seller, Product Product, CartItem? CartItem)> SeedCartAsync(
        AppDbContext db, SellerStatus sellerStatus, bool includeCartItem = false)
    {
        var key = Guid.NewGuid().ToString("N");
        var buyer = new User
        {
            Email = $"buyer-{key}@example.invalid",
            PasswordHash = "test",
            FullName = "Fixture buyer",
            Role = UserRole.Customer
        };
        var seller = new User
        {
            Email = $"seller-{key}@example.invalid",
            PasswordHash = "test",
            FullName = "Fixture seller",
            Role = UserRole.Seller,
            SellerStatus = sellerStatus
        };
        var category = new Category { Name = $"Category-{key}" };
        db.AddRange(buyer, seller, category);
        await db.SaveChangesAsync();

        var product = new Product
        {
            Name = "Fixture product",
            Sku = $"SKU-{key}",
            Price = 10m,
            Stock = 10,
            SellerId = seller.Id,
            CategoryId = category.Id
        };
        db.Products.Add(product);
        await db.SaveChangesAsync();

        CartItem? cartItem = null;
        if (includeCartItem)
        {
            var cart = new Cart { UserId = buyer.Id };
            cartItem = new CartItem { ProductId = product.Id, Quantity = 2 };
            cart.Items.Add(cartItem);
            db.Carts.Add(cart);
            await db.SaveChangesAsync();
        }

        return (buyer, seller, product, cartItem);
    }

    private static async Task<(User Buyer, Product ApprovedProduct, Product PendingProduct)> SeedCheckoutCartAsync(
        AppDbContext db)
    {
        var key = Guid.NewGuid().ToString("N");
        var buyer = new User
        {
            Email = $"buyer-{key}@example.invalid",
            PasswordHash = "test",
            FullName = "Fixture buyer",
            Role = UserRole.Customer
        };
        var approvedSeller = new User
        {
            Email = $"approved-{key}@example.invalid",
            PasswordHash = "test",
            FullName = "Approved seller",
            Role = UserRole.Seller,
            SellerStatus = SellerStatus.Approved
        };
        var pendingSeller = new User
        {
            Email = $"pending-{key}@example.invalid",
            PasswordHash = "test",
            FullName = "Pending seller",
            Role = UserRole.Seller,
            SellerStatus = SellerStatus.Pending
        };
        var category = new Category { Name = $"Checkout-category-{key}" };
        db.AddRange(buyer, approvedSeller, pendingSeller, category);
        await db.SaveChangesAsync();

        var approvedProduct = new Product
        {
            Name = "Approved product",
            Sku = $"APPROVED-{key}",
            Price = 10m,
            Stock = 10,
            SellerId = approvedSeller.Id,
            CategoryId = category.Id
        };
        var pendingProduct = new Product
        {
            Name = "Pending product",
            Sku = $"PENDING-{key}",
            Price = 10m,
            Stock = 10,
            SellerId = pendingSeller.Id,
            CategoryId = category.Id
        };
        db.Products.AddRange(approvedProduct, pendingProduct);
        await db.SaveChangesAsync();

        var cart = new Cart { UserId = buyer.Id };
        cart.Items.Add(new CartItem { ProductId = approvedProduct.Id, Quantity = 2 });
        cart.Items.Add(new CartItem { ProductId = pendingProduct.Id, Quantity = 1 });
        db.Carts.Add(cart);
        await db.SaveChangesAsync();

        return (buyer, approvedProduct, pendingProduct);
    }
}
