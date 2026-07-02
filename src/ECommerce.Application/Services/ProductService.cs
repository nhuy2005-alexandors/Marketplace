using ECommerce.Application.Common;
using ECommerce.Application.DTOs.Catalog;
using ECommerce.Application.Interfaces;
using ECommerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Application.Services;

public class ProductService : IProductService
{
    private readonly IAppDbContext _db;

    public ProductService(IAppDbContext db) => _db = db;

    public async Task<PagedResult<ProductDto>> SearchAsync(ProductQuery q, CancellationToken ct = default)
    {
        var query = _db.Products
            .Include(p => p.Category)
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
        if (q.MinPrice.HasValue)
            query = query.Where(p => p.Price >= q.MinPrice.Value);
        if (q.MaxPrice.HasValue)
            query = query.Where(p => p.Price <= q.MaxPrice.Value);

        query = (q.SortBy?.ToLowerInvariant()) switch
        {
            "price" => q.Desc ? query.OrderByDescending(p => p.Price) : query.OrderBy(p => p.Price),
            "name" => q.Desc ? query.OrderByDescending(p => p.Name) : query.OrderBy(p => p.Name),
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
            .Include(p => p.Category)
            .Include(p => p.Reviews)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
        return product is null
            ? Result.Fail<ProductDto>("Product not found.", ErrorType.NotFound)
            : Result.Ok(product.ToDto());
    }

    public async Task<Result<ProductDto>> CreateAsync(CreateProductRequest r, CancellationToken ct = default)
    {
        if (!await _db.Categories.AnyAsync(c => c.Id == r.CategoryId, ct))
            return Result.Fail<ProductDto>("Category not found.", ErrorType.Validation);

        var product = new Product
        {
            Name = r.Name.Trim(),
            Description = r.Description,
            Price = r.Price,
            Stock = r.Stock,
            ImageUrl = r.ImageUrl,
            CategoryId = r.CategoryId
        };
        _db.Products.Add(product);
        await _db.SaveChangesAsync(ct);
        await _db.Entry(product).Reference(p => p.Category).LoadAsync(ct);
        return Result.Ok(product.ToDto());
    }

    public async Task<Result<ProductDto>> UpdateAsync(int id, UpdateProductRequest r, CancellationToken ct = default)
    {
        var product = await _db.Products
            .Include(p => p.Category)
            .Include(p => p.Reviews)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
        if (product is null)
            return Result.Fail<ProductDto>("Product not found.", ErrorType.NotFound);
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

    public async Task<Result> DeleteAsync(int id, CancellationToken ct = default)
    {
        var product = await _db.Products.FindAsync(new object[] { id }, ct);
        if (product is null)
            return Result.Fail("Product not found.", ErrorType.NotFound);
        _db.Products.Remove(product);
        await _db.SaveChangesAsync(ct);
        return Result.Ok();
    }
}
