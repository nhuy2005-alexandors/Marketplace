using ECommerce.Application.DTOs.Recommendations;
using ECommerce.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.API.Controllers;

[ApiController]
[Route("api/recommendations")]
public sealed class RecommendationsController : ControllerBase
{
    private const int DefaultLimit = 8;
    private const int MaximumLimit = 20;

    private readonly IRecommendationService _recommendations;
    private readonly ICurrentUser _currentUser;

    public RecommendationsController(IRecommendationService recommendations, ICurrentUser currentUser)
    {
        _recommendations = recommendations;
        _currentUser = currentUser;
    }

    [HttpGet("personalized")]
    [ProducesResponseType(typeof(RecommendationResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<RecommendationResponseDto>> Personalized(
        [FromQuery] int? limit,
        CancellationToken cancellationToken)
    {
        var userId = _currentUser.IsAuthenticated ? _currentUser.UserId : null;
        var result = await _recommendations.GetForUserAsync(
            userId,
            NormalizeLimit(limit),
            cancellationToken: cancellationToken);
        return Ok(result);
    }

    [HttpGet("similar-products/{productId:int}")]
    [ProducesResponseType(typeof(RecommendationResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<RecommendationResponseDto>> SimilarProducts(
        int productId,
        [FromQuery] int? limit,
        CancellationToken cancellationToken)
    {
        var result = await _recommendations.GetSimilarAsync(
            productId,
            NormalizeLimit(limit),
            cancellationToken: cancellationToken);
        return Ok(result);
    }

    private static int NormalizeLimit(int? limit) => Math.Clamp(limit ?? DefaultLimit, 1, MaximumLimit);
}
