using ECommerce.Application.Common;
using ECommerce.Application.DTOs.Catalog;
using ECommerce.Application.Interfaces;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Application.Services;

public class ProductService : IProductService
{
    private readonly IAppDbContext _db;

    public ProductService(IAppDbContext db) => _db = db;

    public async Task<PagedResult<ProductDto>> SearchAsync(ProductQuery q, CancellationToken ct = default)
    {
        var query = _db.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Seller)
            .Include(p => p.Reviews)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var term = q.Search.Trim();
            query = query.Where(p => p.Name.Contains(term) ||
                                     (p.Description != null && p.Description.Contains(term)));
        }
        if (q.CategoryId.HasValue)
            query = query.Where(p => p.CategoryId == q.CategoryId.Value);
        if (q.SellerId.HasValue)
            query = query.Where(p => p.SellerId == q.SellerId.Value);
        if (q.MinPrice.HasValue)
            query = query.Where(p => p.Price >= q.MinPrice.Value);
        if (q.MaxPrice.HasValue)
            query = query.Where(p => p.Price <= q.MaxPrice.Value);

        query = (q.SortBy?.ToLowerInvariant()) switch
        {
            "price" => q.Desc ? query.OrderByDescending(p => p.Price) : query.OrderBy(p => p.Price),
            "name" => q.Desc ? query.OrderByDescending(p => p.Name) : query.OrderBy(p => p.Name),
            "rating" => q.Desc
                ? query.OrderByDescending(p => p.Reviews.Any() ? p.Reviews.Average(r => r.Rating) : 0)
                : query.OrderBy(p => p.Reviews.Any() ? p.Reviews.Average(r => r.Rating) : 0),
            "stock" => q.Desc ? query.OrderByDescending(p => p.Stock) : query.OrderBy(p => p.Stock),
            _ => q.Desc ? query.OrderByDescending(p => p.CreatedAt) : query.OrderBy(p => p.CreatedAt)
        };

        var total = await query.CountAsync(ct);
        var page = Math.Max(1, q.Page);
        var size = Math.Clamp(q.PageSize, 1, 100);

        var items = await query
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(ct);

        return new PagedResult<ProductDto>
        {
            Items = items.Select(p => p.ToDto()).ToList(),
            Page = page,
            PageSize = size,
            TotalCount = total
        };
    }

    public async Task<Result<ProductDto>> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var product = await _db.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Seller)
            .Include(p => p.Reviews)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
        return product is null
            ? Result.Fail<ProductDto>("Product not found.", ErrorType.NotFound)
            : Result.Ok(product.ToDto());
    }

    public async Task<Result<ProductDto>> CreateAsync(int sellerId, CreateProductRequest r, CancellationToken ct = default)
    {
        // Seller chưa được Admin duyệt thì không đăng sản phẩm. Admin (SellerStatus null) bỏ qua.
        var actor = await _db.Users.AsNoTracking()
            .Where(u => u.Id == sellerId)
            .Select(u => new { u.Role, u.SellerStatus })
            .FirstOrDefaultAsync(ct);
        if (actor is null)
            return Result.Fail<ProductDto>("User not found.", ErrorType.NotFound);
        if (actor is { Role: UserRole.Seller, SellerStatus: not SellerStatus.Approved })
            return Result.Fail<ProductDto>("Tài khoản seller chưa được duyệt.", ErrorType.Forbidden);

        if (!await _db.Categories.AnyAsync(c => c.Id == r.CategoryId, ct))
            return Result.Fail<ProductDto>("Category not found.", ErrorType.Validation);

        var product = new Product
        {
            Name = r.Name.Trim(),
            Description = r.Description,
            Price = r.Price,
            Stock = r.Stock,
            ImageUrl = r.ImageUrl,
            CategoryId = r.CategoryId,
            SellerId = sellerId
        };
        _db.Products.Add(product);
        await _db.SaveChangesAsync(ct);
        await _db.Entry(product).Reference(p => p.Category).LoadAsync(ct);
        await _db.Entry(product).Reference(p => p.Seller).LoadAsync(ct);
        return Result.Ok(product.ToDto());
    }

    public async Task<Result<ProductDto>> UpdateAsync(int actorId, bool isAdmin, int id, UpdateProductRequest r, CancellationToken ct = default)
    {
        var product = await _db.Products
            .Include(p => p.Category)
            .Include(p => p.Seller)
            .Include(p => p.Reviews)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
        if (product is null)
            return Result.Fail<ProductDto>("Product not found.", ErrorType.NotFound);
        if (!isAdmin && product.SellerId != actorId)
            return Result.Fail<ProductDto>("You can only edit your own products.", ErrorType.Forbidden);
        if (!await _db.Categories.AnyAsync(c => c.Id == r.CategoryId, ct))
            return Result.Fail<ProductDto>("Category not found.", ErrorType.Validation);

        product.Name = r.Name.Trim();
        product.Description = r.Description;
        product.Price = r.Price;
        product.Stock = r.Stock;
        product.ImageUrl = r.ImageUrl;
        product.CategoryId = r.CategoryId;
        product.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _db.Entry(product).Reference(p => p.Category).LoadAsync(ct);
        return Result.Ok(product.ToDto());
    }

    public async Task<Result> DeleteAsync(int actorId, bool isAdmin, int id, CancellationToken ct = default)
    {
        var product = await _db.Products.FindAsync(new object[] { id }, ct);
        if (product is null)
            return Result.Fail("Product not found.", ErrorType.NotFound);
        if (!isAdmin && product.SellerId != actorId)
            return Result.Fail("You can only delete your own products.", ErrorType.Forbidden);
        _db.Products.Remove(product);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Product đã nằm trong đơn hàng (FK Restrict) — không xóa được, gợi ý ẩn thay vì xóa.
            return Result.Fail("Không thể xóa sản phẩm đã có trong đơn hàng.", ErrorType.Conflict);
        }
        return Result.Ok();
    }

    public async Task<Result<SellerShopDto>> GetSellerShopAsync(int sellerId, CancellationToken ct = default)
    {
        var seller = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == sellerId && u.Role == UserRole.Seller
                        && u.SellerStatus == SellerStatus.Approved)
            .Select(u => new { u.Id, u.ShopName, u.FullName })
            .FirstOrDefaultAsync(ct);
        return seller is null
            ? Result.Fail<SellerShopDto>("Seller not found.", ErrorType.NotFound)
            : Result.Ok(new SellerShopDto(seller.Id, seller.ShopName ?? seller.FullName));
    }
}
