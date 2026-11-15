using ECommerce.Application.DTOs.Catalog;

namespace ECommerce.Application.DTOs.Recommendations;

public sealed record RecommendationReasonDto(string Code, string Label);

public sealed record RecommendedProductDto(
    ProductDto Product,
    double Score,
    IReadOnlyList<RecommendationReasonDto> Reasons);

public sealed record RecommendationResponseDto(
    string Strategy,
    DateTime GeneratedAtUtc,
    IReadOnlyList<RecommendedProductDto> Items);
