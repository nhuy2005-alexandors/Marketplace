using ECommerce.Application.DTOs.Catalog;
using ECommerce.Application.Interfaces;

namespace ECommerce.Infrastructure.AI;

/// <summary>RAG adapter over the same application recommendation service used by the Recommendation API.</summary>
public sealed class RecommendationProviderAdapter(IRecommendationService recommendations) : IRecommendationProvider
{
    public async Task<IReadOnlyList<ProductDto>> GetProductsAsync(int? customerId, int limit, CancellationToken ct = default, IReadOnlyCollection<int>? eligibleProductIds = null)
    {
        var response = await recommendations.GetForUserAsync(customerId, Math.Clamp(limit, 1, 20), cancellationToken: ct, eligibleProductIds: eligibleProductIds);
        return response.Items.Select(item => item.Product).ToArray();
    }

    public async Task<IReadOnlyList<ProductDto>> GetSimilarProductsAsync(int targetProductId, int limit, CancellationToken ct = default, IReadOnlyCollection<int>? eligibleProductIds = null)
    {
        var response = await recommendations.GetSimilarAsync(targetProductId, Math.Clamp(limit, 1, 20), cancellationToken: ct, eligibleProductIds: eligibleProductIds);
        return response.Items.Select(item => item.Product).ToArray();
    }
}
