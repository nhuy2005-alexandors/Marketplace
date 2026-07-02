using ECommerce.Application.Common;
using ECommerce.Application.DTOs.Cart;
using ECommerce.Application.Interfaces;
using ECommerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Application.Services;

public class CartService : ICartService
{
    private readonly IAppDbContext _db;

    public CartService(IAppDbContext db) => _db = db;

    public async Task<CartDto> GetAsync(int userId, CancellationToken ct = default)
    {
        var cart = await LoadCartAsync(userId, ct);
        return cart.ToDto();
    }

    public async Task<Result<CartDto>> AddItemAsync(int userId, AddCartItemRequest r, CancellationToken ct = default)
    {
        if (r.Quantity <= 0)
            return Result.Fail<CartDto>("Quantity must be positive.");
        var product = await _db.Products.FindAsync(new object[] { r.ProductId }, ct);
        if (product is null)
            return Result.Fail<CartDto>("Product not found.", ErrorType.NotFound);

        var cart = await LoadCartAsync(userId, ct);
        var item = cart.Items.FirstOrDefault(i => i.ProductId == r.ProductId);
        var newQty = (item?.Quantity ?? 0) + r.Quantity;
        if (newQty > product.Stock)
            return Result.Fail<CartDto>($"Only {product.Stock} in stock.", ErrorType.Validation);

        if (item is null)
        {
            item = new CartItem { CartId = cart.Id, ProductId = r.ProductId, Quantity = r.Quantity };
            cart.Items.Add(item);
            _db.CartItems.Add(item);
        }
        else
        {
            item.Quantity = newQty;
        }
        await _db.SaveChangesAsync(ct);
        return Result.Ok((await LoadCartAsync(userId, ct)).ToDto());
    }

    public async Task<Result<CartDto>> UpdateItemAsync(int userId, int itemId, UpdateCartItemRequest r, CancellationToken ct = default)
    {
        var cart = await LoadCartAsync(userId, ct);
        var item = cart.Items.FirstOrDefault(i => i.Id == itemId);
        if (item is null)
            return Result.Fail<CartDto>("Cart item not found.", ErrorType.NotFound);
        if (r.Quantity <= 0)
        {
            _db.CartItems.Remove(item);
        }
        else
        {
            if (r.Quantity > item.Product.Stock)
                return Result.Fail<CartDto>($"Only {item.Product.Stock} in stock.", ErrorType.Validation);
            item.Quantity = r.Quantity;
        }
        await _db.SaveChangesAsync(ct);
        return Result.Ok((await LoadCartAsync(userId, ct)).ToDto());
    }

    public async Task<Result<CartDto>> RemoveItemAsync(int userId, int itemId, CancellationToken ct = default)
    {
        var cart = await LoadCartAsync(userId, ct);
        var item = cart.Items.FirstOrDefault(i => i.Id == itemId);
        if (item is null)
            return Result.Fail<CartDto>("Cart item not found.", ErrorType.NotFound);
        _db.CartItems.Remove(item);
        await _db.SaveChangesAsync(ct);
        return Result.Ok((await LoadCartAsync(userId, ct)).ToDto());
    }

    public async Task<Result> ClearAsync(int userId, CancellationToken ct = default)
    {
        var cart = await LoadCartAsync(userId, ct);
        foreach (var item in cart.Items.ToList())
            _db.CartItems.Remove(item);
        await _db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    private async Task<Cart> LoadCartAsync(int userId, CancellationToken ct)
    {
        var cart = await _db.Carts
            .Include(c => c.Items).ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(c => c.UserId == userId, ct);
        if (cart is null)
        {
            cart = new Cart { UserId = userId };
            _db.Carts.Add(cart);
            await _db.SaveChangesAsync(ct);
        }
        return cart;
    }
}
