using ECommerce.Application.Common;
using ECommerce.Application.DTOs.Catalog;
using ECommerce.Application.Interfaces;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Application.Services;

public class ProductService : IProductService
{
    private const int MaxAttributesPerProduct = 100;
    private const int MaxAttributeFilters = 20;
    private const decimal MaxPrice = 9999999999999999.99m;
    private const decimal MaxAttributeNumber = 99999999999999.9999m;

    private readonly IAppDbContext _db;

    public ProductService(IAppDbContext db) => _db = db;

    public async Task<PagedResult<ProductDto>> SearchAsync(ProductQuery q, CancellationToken ct = default)
    {
        var query = PublicCatalogQuery();

        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var term = q.Search.Trim();
            query = query.Where(p => p.Name.Contains(term) ||
                                     (p.Description != null && p.Description.Contains(term)) ||
                                     (p.Brand != null && p.Brand.Name.Contains(term)));
        }
        if (q.CategoryId.HasValue) query = query.Where(p => p.CategoryId == q.CategoryId.Value);
        if (q.SellerId.HasValue) query = query.Where(p => p.SellerId == q.SellerId.Value);
        if (q.BrandId.HasValue) query = query.Where(p => p.BrandId == q.BrandId.Value);
        if (q.MinPrice.HasValue) query = query.Where(p => p.Price >= q.MinPrice.Value);
        if (q.MaxPrice.HasValue) query = query.Where(p => p.Price <= q.MaxPrice.Value);
        if (q.InStockOnly) query = query.Where(p => p.Stock > 0);

        var filters = q.AttributeFilters ?? new List<ProductAttributeFilter>();
        foreach (var filter in filters.Take(MaxAttributeFilters))
        {
            var attributeId = filter.AttributeDefinitionId;
            if (!string.IsNullOrWhiteSpace(filter.ValueText))
            {
                var value = filter.ValueText.Trim();
                query = query.Where(p => p.AttributeValues.Any(v =>
                    v.AttributeDefinitionId == attributeId &&
                    v.AttributeDefinition.IsActive && v.AttributeDefinition.IsFilterable &&
                    v.ValueText == value));
            }
            else if (filter.MinValue.HasValue || filter.MaxValue.HasValue)
            {
                var min = filter.MinValue;
                var max = filter.MaxValue;
                query = query.Where(p => p.AttributeValues.Any(v =>
                    v.AttributeDefinitionId == attributeId &&
                    v.AttributeDefinition.IsActive && v.AttributeDefinition.IsFilterable &&
                    v.ValueNumber.HasValue &&
                    (!min.HasValue || v.ValueNumber.Value >= min.Value) &&
                    (!max.HasValue || v.ValueNumber.Value <= max.Value)));
            }
            else if (filter.ValueBoolean.HasValue)
            {
                var value = filter.ValueBoolean.Value;
                query = query.Where(p => p.AttributeValues.Any(v =>
                    v.AttributeDefinitionId == attributeId &&
                    v.AttributeDefinition.IsActive && v.AttributeDefinition.IsFilterable &&
                    v.ValueBoolean == value));
            }
        }

        query = (q.SortBy?.ToLowerInvariant()) switch
        {
            "price" => q.Desc ? query.OrderByDescending(p => p.Price).ThenByDescending(p => p.Id) : query.OrderBy(p => p.Price).ThenBy(p => p.Id),
            "name" => q.Desc ? query.OrderByDescending(p => p.Name).ThenByDescending(p => p.Id) : query.OrderBy(p => p.Name).ThenBy(p => p.Id),
            "rating" => q.Desc
                ? query.OrderByDescending(p => p.Reviews.Any() ? p.Reviews.Average(r => r.Rating) : 0).ThenByDescending(p => p.Id)
                : query.OrderBy(p => p.Reviews.Any() ? p.Reviews.Average(r => r.Rating) : 0).ThenBy(p => p.Id),
            "stock" => q.Desc ? query.OrderByDescending(p => p.Stock).ThenByDescending(p => p.Id) : query.OrderBy(p => p.Stock).ThenBy(p => p.Id),
            _ => q.Desc ? query.OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id) : query.OrderBy(p => p.CreatedAt).ThenBy(p => p.Id)
        };

        var total = await query.CountAsync(ct);
        var page = Math.Max(1, q.Page);
        var size = Math.Clamp(q.PageSize, 1, 100);
        var items = await query.Skip((page - 1) * size).Take(size).ToListAsync(ct);

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
        var product = await PublicCatalogQuery().FirstOrDefaultAsync(p => p.Id == id, ct);
        return product is null
            ? Result.Fail<ProductDto>("Product not found.", ErrorType.NotFound)
            : Result.Ok(product.ToDto());
    }

    public async Task<Result<ProductDto>> CreateAsync(int sellerId, CreateProductRequest r, CancellationToken ct = default)
    {
        var validation = ValidateBasics(r.Name, r.Description, r.Price, r.Stock, r.ImageUrl);
        if (validation is not null)
            return Result.Fail<ProductDto>(validation, ErrorType.Validation);

        var actor = await _db.Users.AsNoTracking()
            .Where(u => u.Id == sellerId)
            .Select(u => new { u.Role, u.SellerStatus })
            .FirstOrDefaultAsync(ct);
        if (actor is null)
            return Result.Fail<ProductDto>("User not found.", ErrorType.NotFound);
        if (actor.Role is not (UserRole.Admin or UserRole.Seller))
            return Result.Fail<ProductDto>("Only admins and approved sellers can create products.", ErrorType.Forbidden);
        if (actor.Role == UserRole.Seller && actor.SellerStatus != SellerStatus.Approved)
            return Result.Fail<ProductDto>("Tài khoản seller chưa được duyệt.", ErrorType.Forbidden);

        if (!await _db.Categories.AnyAsync(c => c.Id == r.CategoryId, ct))
            return Result.Fail<ProductDto>("Category not found.", ErrorType.Validation);
        if (r.BrandId.HasValue && !await _db.Brands.AnyAsync(b => b.Id == r.BrandId.Value, ct))
            return Result.Fail<ProductDto>("Brand not found.", ErrorType.Validation);

        var sku = r.Sku is null ? GenerateSku(sellerId) : NormalizeSku(r.Sku);
        if (sku is null)
            return Result.Fail<ProductDto>("SKU must contain 1 to 100 non-whitespace characters.", ErrorType.Validation);
        if (await _db.Products.AnyAsync(p => p.Sku == sku, ct))
            return Result.Fail<ProductDto>("SKU already exists.", ErrorType.Conflict);

        var attributes = await BuildAttributeValuesAsync(r.CategoryId, r.Attributes, ct);
        if (!attributes.Success)
            return Result.Fail<ProductDto>(attributes.Error!, attributes.ErrorType);

        var product = new Product
        {
            Name = r.Name.Trim(),
            Sku = sku,
            BrandId = r.BrandId,
            Description = r.Description?.Trim(),
            Price = r.Price,
            Stock = r.Stock,
            ImageUrl = r.ImageUrl?.Trim(),
            CategoryId = r.CategoryId,
            SellerId = sellerId
        };
        foreach (var value in attributes.Value!)
        {
            value.Product = product;
            product.AttributeValues.Add(value);
        }
        _db.Products.Add(product);
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Fail<ProductDto>("Catalog data changed concurrently. Reload and retry.", ErrorType.Conflict);
        }
        catch (DbUpdateException)
        {
            return Result.Fail<ProductDto>("SKU already exists or product violates a catalog constraint.", ErrorType.Conflict);
        }
        return await GetByIdAsync(product.Id, ct);
    }

    public async Task<Result<ProductDto>> UpdateAsync(
        int actorId, bool isAdmin, int id, UpdateProductRequest r, CancellationToken ct = default)
    {
        var product = await _db.Products
            .Include(p => p.Category)
            .Include(p => p.Seller)
            .Include(p => p.Brand)
            .Include(p => p.Reviews)
            .Include(p => p.AttributeValues).ThenInclude(v => v.AttributeDefinition)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
        if (product is null)
            return Result.Fail<ProductDto>("Product not found.", ErrorType.NotFound);

        if (!isAdmin)
        {
            var actor = await _db.Users.AsNoTracking()
                .Where(u => u.Id == actorId)
                .Select(u => new { u.Role, u.SellerStatus })
                .FirstOrDefaultAsync(ct);
            if (actor is null || actor.Role != UserRole.Seller || actor.SellerStatus != SellerStatus.Approved)
                return Result.Fail<ProductDto>("Only approved sellers can manage products.", ErrorType.Forbidden);
            if (product.SellerId != actorId)
                return Result.Fail<ProductDto>("You can only edit your own products.", ErrorType.Forbidden);
        }

        var validation = ValidateBasics(r.Name, r.Description, r.Price, r.Stock, r.ImageUrl);
        if (validation is not null)
            return Result.Fail<ProductDto>(validation, ErrorType.Validation);
        if (r.BrandId.HasValue && r.ClearBrand)
            return Result.Fail<ProductDto>("BrandId and ClearBrand cannot be supplied together.", ErrorType.Validation);

        // CategoryId participates in the composite principal key used by ProductAttributeValues.
        if (r.CategoryId != product.CategoryId)
            return Result.Fail<ProductDto>(
                "Product category is immutable after creation. Create a new product in the target category.",
                ErrorType.Conflict);
        if (r.BrandId.HasValue && !await _db.Brands.AnyAsync(b => b.Id == r.BrandId.Value, ct))
            return Result.Fail<ProductDto>("Brand not found.", ErrorType.Validation);

        var sku = product.Sku;
        if (r.Sku is not null)
        {
            sku = NormalizeSku(r.Sku);
            if (sku is null)
                return Result.Fail<ProductDto>("SKU must contain 1 to 100 non-whitespace characters.", ErrorType.Validation);
            if (await _db.Products.AnyAsync(p => p.Id != id && p.Sku == sku, ct))
                return Result.Fail<ProductDto>("SKU already exists.", ErrorType.Conflict);
        }

        if (r.Attributes is not null)
        {
            var attributes = await BuildAttributeValuesAsync(r.CategoryId, r.Attributes, ct);
            if (!attributes.Success)
                return Result.Fail<ProductDto>(attributes.Error!, attributes.ErrorType);

            var existing = product.AttributeValues.ToDictionary(v => v.AttributeDefinitionId);
            var incomingIds = attributes.Value!.Select(v => v.AttributeDefinitionId).ToHashSet();

            foreach (var oldValue in existing.Values.Where(v => !incomingIds.Contains(v.AttributeDefinitionId)).ToList())
            {
                product.AttributeValues.Remove(oldValue);
                _db.ProductAttributeValues.Remove(oldValue);
            }

            foreach (var incoming in attributes.Value!)
            {
                if (existing.TryGetValue(incoming.AttributeDefinitionId, out var current))
                {
                    current.ValueText = incoming.ValueText;
                    current.ValueNumber = incoming.ValueNumber;
                    current.ValueBoolean = incoming.ValueBoolean;
                }
                else
                {
                    incoming.ProductId = product.Id;
                    incoming.Product = product;
                    product.AttributeValues.Add(incoming);
                }
            }
        }

        product.Name = r.Name.Trim();
        product.Sku = sku;
        if (r.BrandId.HasValue)
        {
            product.BrandId = r.BrandId.Value;
            product.Brand = await _db.Brands.FindAsync(new object[] { r.BrandId.Value }, ct);
        }
        else if (r.ClearBrand)
        {
            product.BrandId = null;
            product.Brand = null;
        }
        product.Description = r.Description?.Trim();
        product.Price = r.Price;
        product.Stock = r.Stock;
        product.ImageUrl = r.ImageUrl?.Trim();
        product.UpdatedAt = DateTime.UtcNow;

        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Fail<ProductDto>("Product changed concurrently. Reload and retry.", ErrorType.Conflict);
        }
        catch (DbUpdateException)
        {
            return Result.Fail<ProductDto>("SKU already exists or product violates a catalog constraint.", ErrorType.Conflict);
        }
        return Result.Ok(product.ToDto());
    }

    public async Task<Result> DeleteAsync(int actorId, bool isAdmin, int id, CancellationToken ct = default)
    {
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (product is null)
            return Result.Fail("Product not found.", ErrorType.NotFound);

        if (!isAdmin)
        {
            var actor = await _db.Users.AsNoTracking()
                .Where(u => u.Id == actorId)
                .Select(u => new { u.Role, u.SellerStatus })
                .FirstOrDefaultAsync(ct);
            if (actor is null || actor.Role != UserRole.Seller || actor.SellerStatus != SellerStatus.Approved)
                return Result.Fail("Only approved sellers can manage products.", ErrorType.Forbidden);
            if (product.SellerId != actorId)
                return Result.Fail("You can only delete your own products.", ErrorType.Forbidden);
        }

        var attributeValues = await _db.ProductAttributeValues.Where(v => v.ProductId == id).ToListAsync(ct);
        _db.ProductAttributeValues.RemoveRange(attributeValues);
        _db.Products.Remove(product);
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Fail("Product changed concurrently. Reload and retry.", ErrorType.Conflict);
        }
        catch (DbUpdateException)
        {
            return Result.Fail("Không thể xóa sản phẩm đã có trong đơn hàng hoặc dữ liệu catalog.", ErrorType.Conflict);
        }
        return Result.Ok();
    }

    public async Task<Result<SellerShopDto>> GetSellerShopAsync(int sellerId, CancellationToken ct = default)
    {
        var seller = await _db.Users.AsNoTracking()
            .Where(u => u.Id == sellerId && u.Role == UserRole.Seller && u.SellerStatus == SellerStatus.Approved)
            .Select(u => new { u.Id, u.ShopName, u.FullName })
            .FirstOrDefaultAsync(ct);
        return seller is null
            ? Result.Fail<SellerShopDto>("Seller not found.", ErrorType.NotFound)
            : Result.Ok(new SellerShopDto(seller.Id, seller.ShopName ?? seller.FullName));
    }

    private IQueryable<Product> PublicCatalogQuery() =>
        _db.Products.AsNoTracking()
            .Where(p => p.Seller.Role != UserRole.Seller || p.Seller.SellerStatus == SellerStatus.Approved)
            .Include(p => p.Category)
            .Include(p => p.Seller)
            .Include(p => p.Brand)
            .Include(p => p.Reviews)
            .Include(p => p.AttributeValues).ThenInclude(v => v.AttributeDefinition);

    private async Task<Result<List<ProductAttributeValue>>> BuildAttributeValuesAsync(
        int categoryId, IReadOnlyList<ProductAttributeInput>? inputs, CancellationToken ct)
    {
        inputs ??= Array.Empty<ProductAttributeInput>();
        if (inputs.Count > MaxAttributesPerProduct)
            return Result.Fail<List<ProductAttributeValue>>(
                $"A product can contain at most {MaxAttributesPerProduct} structured attributes.", ErrorType.Validation);

        var ids = inputs.Select(x => x.AttributeDefinitionId).ToArray();
        if (ids.Any(id => id <= 0) || ids.Distinct().Count() != ids.Length)
            return Result.Fail<List<ProductAttributeValue>>(
                "Attribute definition IDs must be positive and unique.", ErrorType.Validation);

        var definitions = await _db.AttributeDefinitions
            .Where(a => ids.Contains(a.Id) && a.CategoryId == categoryId && a.IsActive)
            .ToDictionaryAsync(a => a.Id, ct);
        if (definitions.Count != ids.Length)
            return Result.Fail<List<ProductAttributeValue>>(
                "One or more attributes are inactive, unknown or belong to another category.", ErrorType.Validation);

        var requiredIds = await _db.AttributeDefinitions
            .Where(a => a.CategoryId == categoryId && a.IsActive && a.IsRequired)
            .Select(a => a.Id).ToListAsync(ct);
        if (requiredIds.Any(id => !ids.Contains(id)))
            return Result.Fail<List<ProductAttributeValue>>(
                "All required category attributes must be supplied.", ErrorType.Validation);

        var values = new List<ProductAttributeValue>(inputs.Count);
        foreach (var input in inputs)
        {
            var definition = definitions[input.AttributeDefinitionId];
            var hasText = input.ValueText is not null;
            var hasNumber = input.ValueNumber.HasValue;
            var hasBoolean = input.ValueBoolean.HasValue;
            var suppliedCount = (hasText ? 1 : 0) + (hasNumber ? 1 : 0) + (hasBoolean ? 1 : 0);
            if (suppliedCount != 1)
                return Result.Fail<List<ProductAttributeValue>>(
                    $"Attribute '{definition.Code}' must contain exactly one typed value.", ErrorType.Validation);

            var value = new ProductAttributeValue
            {
                AttributeDefinitionId = definition.Id,
                AttributeDefinition = definition,
                CategoryId = definition.CategoryId,
                DataType = definition.DataType
            };

            switch (definition.DataType)
            {
                case CatalogAttributeType.Text:
                    if (!hasText || string.IsNullOrWhiteSpace(input.ValueText) || hasNumber || hasBoolean)
                        return Result.Fail<List<ProductAttributeValue>>(
                            $"Attribute '{definition.Code}' requires a non-empty text value.", ErrorType.Validation);
                    if (input.ValueText.Trim().Length > 2000)
                        return Result.Fail<List<ProductAttributeValue>>(
                            $"Attribute '{definition.Code}' exceeds 2000 characters.", ErrorType.Validation);
                    value.ValueText = input.ValueText.Trim();
                    break;

                case CatalogAttributeType.Number:
                    if (!hasNumber || hasText || hasBoolean)
                        return Result.Fail<List<ProductAttributeValue>>(
                            $"Attribute '{definition.Code}' requires a numeric value.", ErrorType.Validation);
                    if (decimal.Round(input.ValueNumber!.Value, 4, MidpointRounding.AwayFromZero) != input.ValueNumber.Value ||
                        decimal.Abs(input.ValueNumber.Value) > MaxAttributeNumber)
                        return Result.Fail<List<ProductAttributeValue>>(
                            $"Attribute '{definition.Code}' supports at most 4 decimal places and decimal(18,4) range.", ErrorType.Validation);
                    value.ValueNumber = input.ValueNumber;
                    break;

                case CatalogAttributeType.Boolean:
                    if (!hasBoolean || hasText || hasNumber)
                        return Result.Fail<List<ProductAttributeValue>>(
                            $"Attribute '{definition.Code}' requires a boolean value.", ErrorType.Validation);
                    value.ValueBoolean = input.ValueBoolean;
                    break;

                default:
                    return Result.Fail<List<ProductAttributeValue>>("Unsupported attribute data type.", ErrorType.Validation);
            }
            values.Add(value);
        }
        return Result.Ok(values);
    }

    private static string? ValidateBasics(string? name, string? description, decimal price, int stock, string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 200)
            return "Product name must contain 1 to 200 characters.";
        if (description?.Length > 2000)
            return "Product description must not exceed 2000 characters.";
        if (price <= 0 || price > MaxPrice ||
            decimal.Round(price, 2, MidpointRounding.AwayFromZero) != price)
            return "Product price must be positive, within decimal(18,2) range and have at most 2 decimal places.";
        if (stock < 0)
            return "Product stock cannot be negative.";
        if (imageUrl?.Length > 500)
            return "Product image URL must not exceed 500 characters.";
        return null;
    }

    private static string GenerateSku(int sellerId) => $"S{sellerId}-{Guid.NewGuid():N}".ToUpperInvariant();

    private static string? NormalizeSku(string? sku)
    {
        if (sku is null) return null;
        var normalized = sku.Trim().ToUpperInvariant();
        return normalized.Length is > 0 and <= 100 ? normalized : null;
    }
}
