using ECommerce.Application.DTOs.Recommendations;
using ECommerce.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.API.Controllers;

[AllowAnonymous]
public sealed class RecommendationsController(IRecommendationService recommendations) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(RecommendationResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<RecommendationResponseDto>> Get(
        [FromQuery] int? take,
        CancellationToken cancellationToken)
    {
        int? customerId = User.Identity?.IsAuthenticated == true && User.IsInRole("Customer")
            ? UserId
            : null;

        return Ok(await recommendations.GetForUserAsync(customerId, take, cancellationToken));
    }

    [HttpGet("products/{productId:int}/similar")]
    [ProducesResponseType(typeof(RecommendationResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<RecommendationResponseDto>> Similar(
        int productId,
        [FromQuery] int? take,
        CancellationToken cancellationToken) =>
        Ok(await recommendations.GetSimilarAsync(productId, take, cancellationToken));
}
