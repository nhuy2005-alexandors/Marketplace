using ECommerce.Application.DTOs.Recommendations;
using ECommerce.Application.Interfaces;
using ECommerce.Domain.Entities;

namespace ECommerce.Application.Services;

public sealed class RecommendationService(IAppDbContext db) : IRecommendationService
{
    public Task<RecommendationResponseDto> GetForUserAsync(
        int? userId,
        int? take = null,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new RecommendationResponseDto("not_implemented", DateTime.UtcNow, []));

    public Task<RecommendationResponseDto> GetSimilarAsync(
        int targetProductId,
        int? take = null,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new RecommendationResponseDto("not_implemented", DateTime.UtcNow, []));
}
