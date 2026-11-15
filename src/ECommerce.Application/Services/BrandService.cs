using System.Globalization;
using System.Text;
using ECommerce.Application.Common;
using ECommerce.Application.DTOs.Catalog;
using ECommerce.Application.Interfaces;
using ECommerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Application.Services;

public class BrandService : IBrandService
{
    private readonly IAppDbContext _db;

    public BrandService(IAppDbContext db) => _db = db;

    public async Task<IReadOnlyList<BrandDto>> GetAllAsync(CancellationToken ct = default)
    {
        var brands = await _db.Brands.AsNoTracking().OrderBy(b => b.Name).ToListAsync(ct);
        return brands.Select(b => b.ToDto()).ToList();
    }

    public async Task<Result<BrandDto>> CreateAsync(CreateBrandRequest request, CancellationToken ct = default)
    {
        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200)
            return Result.Fail<BrandDto>("Brand name must contain 1 to 200 characters.", ErrorType.Validation);
        if (request.Description?.Length > 2000)
            return Result.Fail<BrandDto>("Brand description must not exceed 2000 characters.", ErrorType.Validation);

        var slug = NormalizeSlug(request.Slug ?? name);
        if (slug is null || slug.Length > 200)
            return Result.Fail<BrandDto>("Brand slug must contain 1 to 200 URL-safe characters.", ErrorType.Validation);
        if (await _db.Brands.AnyAsync(b => b.Slug == slug, ct))
            return Result.Fail<BrandDto>("Brand slug already exists.", ErrorType.Conflict);

        var brand = new Brand { Name = name, Slug = slug, Description = request.Description?.Trim() };
        _db.Brands.Add(brand);
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            return Result.Fail<BrandDto>("Brand slug already exists.", ErrorType.Conflict);
        }
        return Result.Ok(brand.ToDto());
    }

    public async Task<Result<BrandDto>> UpdateAsync(int id, UpdateBrandRequest request, CancellationToken ct = default)
    {
        var brand = await _db.Brands.FirstOrDefaultAsync(b => b.Id == id, ct);
        if (brand is null)
            return Result.Fail<BrandDto>("Brand not found.", ErrorType.NotFound);

        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200)
            return Result.Fail<BrandDto>("Brand name must contain 1 to 200 characters.", ErrorType.Validation);
        if (request.Description?.Length > 2000)
            return Result.Fail<BrandDto>("Brand description must not exceed 2000 characters.", ErrorType.Validation);

        var slug = request.Slug is null ? brand.Slug : NormalizeSlug(request.Slug);
        if (slug is null || slug.Length > 200)
            return Result.Fail<BrandDto>("Brand slug must contain 1 to 200 URL-safe characters.", ErrorType.Validation);
        if (await _db.Brands.AnyAsync(b => b.Id != id && b.Slug == slug, ct))
            return Result.Fail<BrandDto>("Brand slug already exists.", ErrorType.Conflict);

        brand.Name = name;
        brand.Slug = slug;
        brand.Description = request.Description?.Trim();
        brand.UpdatedAt = DateTime.UtcNow;
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            return Result.Fail<BrandDto>("Brand slug already exists.", ErrorType.Conflict);
        }
        return Result.Ok(brand.ToDto());
    }

    public async Task<Result> DeleteAsync(int id, CancellationToken ct = default)
    {
        var brand = await _db.Brands.FirstOrDefaultAsync(b => b.Id == id, ct);
        if (brand is null)
            return Result.Fail("Brand not found.", ErrorType.NotFound);
        if (await _db.Products.AnyAsync(p => p.BrandId == id, ct))
            return Result.Fail("Cannot delete a brand referenced by products.", ErrorType.Conflict);
        _db.Brands.Remove(brand);
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            return Result.Fail("Brand is still referenced by catalog data.", ErrorType.Conflict);
        }
        return Result.Ok();
    }

    private static string? NormalizeSlug(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var source = value.Trim().Replace('đ', 'd').Replace('Đ', 'D').Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(source.Length);
        var separator = false;
        foreach (var c in source)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(c))
            {
                builder.Append(char.ToLowerInvariant(c));
                separator = false;
            }
            else if (builder.Length > 0 && !separator)
            {
                builder.Append('-');
                separator = true;
            }
        }
        var slug = builder.ToString().Trim('-');
        return slug.Length == 0 ? null : slug;
    }
}
