using ECommerce.Application.DTOs.Recommendations;

namespace ECommerce.Application.Interfaces;

public interface IRecommendationService
{
    Task<RecommendationResponseDto> GetForUserAsync(
        int? userId,
        int? take = null,
        CancellationToken cancellationToken = default);

    Task<RecommendationResponseDto> GetSimilarAsync(
        int targetProductId,
        int? take = null,
        CancellationToken cancellationToken = default);
}
