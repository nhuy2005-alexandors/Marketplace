using ECommerce.Domain.Entities;

namespace ECommerce.Application.DTOs.Catalog;

public record CategoryDto(int Id, string Name, string? Description);
public record UploadImageResponse(string Url);
public record SellerShopDto(int SellerId, string ShopName);
public record CreateCategoryRequest(string Name, string? Description);

public record BrandDto(int Id, string Name, string Slug, string? Description);
public record CreateBrandRequest(string Name, string? Slug = null, string? Description = null);
public record UpdateBrandRequest(string Name, string? Slug = null, string? Description = null);

public record AttributeDefinitionDto(
    int Id, int CategoryId, string Code, string DisplayName, CatalogAttributeType DataType,
    string? Unit, bool IsRequired, bool IsFilterable, bool IsComparable, bool IsActive);

public record CreateAttributeDefinitionRequest(
    int CategoryId, string Code, string DisplayName, CatalogAttributeType DataType,
    string? Unit = null, bool IsRequired = false, bool IsFilterable = true,
    bool IsComparable = false, bool IsActive = true);

public record UpdateAttributeDefinitionRequest(
    int CategoryId, string Code, string DisplayName, CatalogAttributeType DataType,
    string? Unit = null, bool IsRequired = false, bool IsFilterable = true,
    bool IsComparable = false, bool IsActive = true);

public record ProductAttributeInput(
    int AttributeDefinitionId, string? ValueText = null, decimal? ValueNumber = null, bool? ValueBoolean = null);

public record ProductAttributeFilter(
    int AttributeDefinitionId, string? ValueText = null, decimal? MinValue = null,
    decimal? MaxValue = null, bool? ValueBoolean = null);

public record ProductAttributeDto(
    int AttributeDefinitionId, string Code, string DisplayName, CatalogAttributeType DataType,
    string? Unit, string? ValueText, decimal? ValueNumber, bool? ValueBoolean);

public record ProductDto(
    int Id, string Name, string? Description, decimal Price, int Stock, string? ImageUrl,
    int CategoryId, string CategoryName, int SellerId, string SellerShopName,
    double AverageRating, int ReviewCount, string Sku = "", int? BrandId = null,
    string? BrandName = null, IReadOnlyList<ProductAttributeDto>? Attributes = null);

public record CreateProductRequest(
    string Name, string? Description, decimal Price, int Stock, string? ImageUrl, int CategoryId,
    string? Sku = null, int? BrandId = null, IReadOnlyList<ProductAttributeInput>? Attributes = null);

public record UpdateProductRequest(
    string Name, string? Description, decimal Price, int Stock, string? ImageUrl, int CategoryId,
    string? Sku = null, int? BrandId = null, IReadOnlyList<ProductAttributeInput>? Attributes = null,
    bool ClearBrand = false);

public record ProductQuery(
    string? Search, int? CategoryId, int? SellerId, decimal? MinPrice, decimal? MaxPrice,
    string? SortBy, bool Desc = false, int Page = 1, int PageSize = 12,
    int? BrandId = null, List<ProductAttributeFilter>? AttributeFilters = null, bool InStockOnly = false);
