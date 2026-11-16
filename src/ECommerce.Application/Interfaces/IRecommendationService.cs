using ECommerce.Application.DTOs.Recommendations;

namespace ECommerce.Application.Interfaces;

public interface IRecommendationService
{
    /// <summary>
    /// Returns recommendations for a customer or a guest. asOfUtc and
    /// cutoffExclusiveUtc are internal service inputs for deterministic evaluation;
    /// they must not be exposed as public API query parameters.
    /// </summary>
    Task<RecommendationResponseDto> GetForUserAsync(
        int? userId,
        int? take = null,
        DateTime? asOfUtc = null,
        DateTime? cutoffExclusiveUtc = null,
        CancellationToken cancellationToken = default, IReadOnlyCollection<int>? eligibleProductIds = null);

    /// <summary>Returns similar in-stock catalog products, excluding the target product.</summary>
    Task<RecommendationResponseDto> GetSimilarAsync(
        int targetProductId,
        int? take = null,
        DateTime? asOfUtc = null,
        DateTime? cutoffExclusiveUtc = null,
        CancellationToken cancellationToken = default, IReadOnlyCollection<int>? eligibleProductIds = null);
}
