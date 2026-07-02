using ECommerce.Application.Common;
using ECommerce.Application.DTOs.Wishlist;
using ECommerce.Application.Interfaces;
using ECommerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Application.Services;

public class WishlistService : IWishlistService
{
    private readonly IAppDbContext _db;

    public WishlistService(IAppDbContext db) => _db = db;

    public async Task<IReadOnlyList<WishlistItemDto>> GetAsync(int userId, CancellationToken ct = default)
    {
        var items = await _db.WishlistItems
            .Include(w => w.Product)
            .Where(w => w.UserId == userId)
            .OrderByDescending(w => w.CreatedAt)
            .ToListAsync(ct);
        return items.Select(w => new WishlistItemDto(
            w.Id, w.ProductId, w.Product.Name, w.Product.Price, w.Product.ImageUrl)).ToList();
    }

    public async Task<Result> AddAsync(int userId, int productId, CancellationToken ct = default)
    {
        if (!await _db.Products.AnyAsync(p => p.Id == productId, ct))
            return Result.Fail("Product not found.", ErrorType.NotFound);
        if (await _db.WishlistItems.AnyAsync(w => w.UserId == userId && w.ProductId == productId, ct))
            return Result.Ok();
        _db.WishlistItems.Add(new WishlistItem { UserId = userId, ProductId = productId });
        await _db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> RemoveAsync(int userId, int productId, CancellationToken ct = default)
    {
        var item = await _db.WishlistItems
            .FirstOrDefaultAsync(w => w.UserId == userId && w.ProductId == productId, ct);
        if (item is null)
            return Result.Fail("Item not in wishlist.", ErrorType.NotFound);
        _db.WishlistItems.Remove(item);
        await _db.SaveChangesAsync(ct);
        return Result.Ok();
    }
}
