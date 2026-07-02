using ECommerce.Application.Common;
using ECommerce.Application.DTOs.Catalog;
using ECommerce.Application.Interfaces;
using ECommerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Application.Services;

public class CategoryService : ICategoryService
{
    private readonly IAppDbContext _db;

    public CategoryService(IAppDbContext db) => _db = db;

    public async Task<IReadOnlyList<CategoryDto>> GetAllAsync(CancellationToken ct = default)
    {
        var categories = await _db.Categories.OrderBy(c => c.Name).ToListAsync(ct);
        return categories.Select(c => c.ToDto()).ToList();
    }

    public async Task<Result<CategoryDto>> CreateAsync(CreateCategoryRequest r, CancellationToken ct = default)
    {
        var name = r.Name.Trim();
        if (await _db.Categories.AnyAsync(c => c.Name == name, ct))
            return Result.Fail<CategoryDto>("Category already exists.", ErrorType.Conflict);
        var category = new Category { Name = name, Description = r.Description };
        _db.Categories.Add(category);
        await _db.SaveChangesAsync(ct);
        return Result.Ok(category.ToDto());
    }

    public async Task<Result> DeleteAsync(int id, CancellationToken ct = default)
    {
        var category = await _db.Categories.FindAsync(new object[] { id }, ct);
        if (category is null)
            return Result.Fail("Category not found.", ErrorType.NotFound);
        if (await _db.Products.AnyAsync(p => p.CategoryId == id, ct))
            return Result.Fail("Cannot delete category with products.", ErrorType.Conflict);
        _db.Categories.Remove(category);
        await _db.SaveChangesAsync(ct);
        return Result.Ok();
    }
}
