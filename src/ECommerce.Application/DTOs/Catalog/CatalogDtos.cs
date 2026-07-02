namespace ECommerce.Application.DTOs.Catalog;

public record CategoryDto(int Id, string Name, string? Description);

public record CreateCategoryRequest(string Name, string? Description);

public record ProductDto(
    int Id,
    string Name,
    string? Description,
    decimal Price,
    int Stock,
    string? ImageUrl,
    int CategoryId,
    string CategoryName,
    int SellerId,
    string SellerShopName,
    double AverageRating,
    int ReviewCount);

public record CreateProductRequest(
    string Name,
    string? Description,
    decimal Price,
    int Stock,
    string? ImageUrl,
    int CategoryId);

public record UpdateProductRequest(
    string Name,
    string? Description,
    decimal Price,
    int Stock,
    string? ImageUrl,
    int CategoryId);

public record ProductQuery(
    string? Search,
    int? CategoryId,
    int? SellerId,
    decimal? MinPrice,
    decimal? MaxPrice,
    string? SortBy,
    bool Desc = false,
    int Page = 1,
    int PageSize = 12);
