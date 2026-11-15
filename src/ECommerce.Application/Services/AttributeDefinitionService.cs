using System.Text.RegularExpressions;
using ECommerce.Application.Common;
using ECommerce.Application.DTOs.Catalog;
using ECommerce.Application.Interfaces;
using ECommerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Application.Services;

public class AttributeDefinitionService : IAttributeDefinitionService
{
    private static readonly Regex CodePattern = new("^[A-Z0-9_-]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private readonly IAppDbContext _db;

    public AttributeDefinitionService(IAppDbContext db) => _db = db;

    public async Task<IReadOnlyList<AttributeDefinitionDto>> GetAllAsync(
        int? categoryId = null, bool includeInactive = false, CancellationToken ct = default)
    {
        var query = _db.AttributeDefinitions.AsNoTracking().AsQueryable();
        if (categoryId.HasValue) query = query.Where(a => a.CategoryId == categoryId.Value);
        if (!includeInactive) query = query.Where(a => a.IsActive);
        var definitions = await query.OrderBy(a => a.CategoryId).ThenBy(a => a.DisplayName).ToListAsync(ct);
        return definitions.Select(a => a.ToDto()).ToList();
    }

    public async Task<Result<AttributeDefinitionDto>> CreateAsync(
        CreateAttributeDefinitionRequest request, CancellationToken ct = default)
    {
        var validation = Validate(request.CategoryId, request.Code, request.DisplayName, request.DataType, request.Unit);
        if (validation is not null)
            return Result.Fail<AttributeDefinitionDto>(validation, ErrorType.Validation);
        if (!await _db.Categories.AnyAsync(c => c.Id == request.CategoryId, ct))
            return Result.Fail<AttributeDefinitionDto>("Category not found.", ErrorType.NotFound);

        var code = request.Code.Trim().ToUpperInvariant();
        if (await _db.AttributeDefinitions.AnyAsync(a => a.CategoryId == request.CategoryId && a.Code == code, ct))
            return Result.Fail<AttributeDefinitionDto>("Attribute code already exists in this category.", ErrorType.Conflict);

        var definition = new AttributeDefinition
        {
            CategoryId = request.CategoryId,
            Code = code,
            DisplayName = request.DisplayName.Trim(),
            DataType = request.DataType,
            Unit = NormalizeUnit(request.Unit),
            IsRequired = request.IsRequired,
            IsFilterable = request.IsFilterable,
            IsComparable = request.IsComparable,
            IsActive = request.IsActive
        };
        _db.AttributeDefinitions.Add(definition);
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            return Result.Fail<AttributeDefinitionDto>("Attribute code already exists in this category.", ErrorType.Conflict);
        }
        return Result.Ok(definition.ToDto());
    }

    public async Task<Result<AttributeDefinitionDto>> UpdateAsync(
        int id, UpdateAttributeDefinitionRequest request, CancellationToken ct = default)
    {
        var definition = await _db.AttributeDefinitions.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (definition is null)
            return Result.Fail<AttributeDefinitionDto>("Attribute definition not found.", ErrorType.NotFound);

        var validation = Validate(request.CategoryId, request.Code, request.DisplayName, request.DataType, request.Unit);
        if (validation is not null)
            return Result.Fail<AttributeDefinitionDto>(validation, ErrorType.Validation);
        if (request.CategoryId != definition.CategoryId || request.DataType != definition.DataType)
            return Result.Fail<AttributeDefinitionDto>(
                "CategoryId and DataType are immutable because they are part of the typed attribute key. Create a new definition instead.",
                ErrorType.Conflict);

        var code = request.Code.Trim().ToUpperInvariant();
        if (await _db.AttributeDefinitions.AnyAsync(a => a.Id != id && a.CategoryId == request.CategoryId && a.Code == code, ct))
            return Result.Fail<AttributeDefinitionDto>("Attribute code already exists in this category.", ErrorType.Conflict);

        definition.Code = code;
        definition.DisplayName = request.DisplayName.Trim();
        definition.Unit = NormalizeUnit(request.Unit);
        definition.IsRequired = request.IsRequired;
        definition.IsFilterable = request.IsFilterable;
        definition.IsComparable = request.IsComparable;
        definition.IsActive = request.IsActive;
        definition.UpdatedAt = DateTime.UtcNow;
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            return Result.Fail<AttributeDefinitionDto>("Attribute code already exists in this category.", ErrorType.Conflict);
        }
        return Result.Ok(definition.ToDto());
    }

    public async Task<Result> DeleteAsync(int id, CancellationToken ct = default)
    {
        var definition = await _db.AttributeDefinitions.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (definition is null)
            return Result.Fail("Attribute definition not found.", ErrorType.NotFound);
        if (await _db.ProductAttributeValues.AnyAsync(v => v.AttributeDefinitionId == id, ct))
            return Result.Fail("Cannot delete an attribute definition referenced by products. Deactivate it instead.", ErrorType.Conflict);
        _db.AttributeDefinitions.Remove(definition);
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            return Result.Fail("Attribute definition is still referenced by catalog data.", ErrorType.Conflict);
        }
        return Result.Ok();
    }

    private static string? Validate(
        int categoryId, string? code, string? displayName, CatalogAttributeType dataType, string? unit)
    {
        if (categoryId <= 0) return "CategoryId must be positive.";
        if (string.IsNullOrWhiteSpace(code) || code.Trim().Length > 100 || !CodePattern.IsMatch(code.Trim().ToUpperInvariant()))
            return "Attribute code must contain 1 to 100 letters, digits, underscores or hyphens.";
        if (string.IsNullOrWhiteSpace(displayName) || displayName.Trim().Length > 200)
            return "Attribute display name must contain 1 to 200 characters.";
        if (!Enum.IsDefined(dataType)) return "Unsupported attribute data type.";
        if (unit?.Length > 40) return "Attribute unit must not exceed 40 characters.";
        return null;
    }

    private static string? NormalizeUnit(string? unit) =>
        string.IsNullOrWhiteSpace(unit) ? null : unit.Trim();
}
