using ECommerce.Application.DTOs.AI;
using ECommerce.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ECommerce.API.Controllers;

[EnableRateLimiting("ai")]
public sealed class AssistantController : ApiControllerBase
{
    private const int MaxRequestBytes = 32 * 1024;
    private readonly IAssistantService _assistant;

    public AssistantController(IAssistantService assistant) => _assistant = assistant;

    [AllowAnonymous]
    [HttpPost("chat")]
    [RequestSizeLimit(MaxRequestBytes)]
    [ProducesResponseType(typeof(AssistantChatResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<AssistantChatResponse>> Chat([FromBody] AssistantChatRequest request, CancellationToken ct)
    {
        var query = request.Query?.Trim();
        if (string.IsNullOrWhiteSpace(query) || query.Length > 1000)
            return BadRequest(new { error = "Query must contain 1 to 1000 characters." });
        if (request.Filters is { MinPrice: < 0 } || request.Filters is { MaxPrice: < 0 } ||
            (request.Filters?.MinPrice is decimal min && request.Filters.MaxPrice is decimal max && min > max))
            return BadRequest(new { error = "Price filters are invalid." });
        if (request.Filters is { CategoryId: <= 0 } || request.Filters is { SellerId: <= 0 })
            return BadRequest(new { error = "Category and seller filters must be positive IDs." });

        var turns = request.Conversation?.RecentTurns;
        if (turns?.Count > 4 || turns?.Any(turn => turn.Content is null || turn.Content.Length > 500 || turn.Role is null || turn.Role.Length > 16) == true)
            return BadRequest(new { error = "Conversation context exceeds the allowed bounds." });
        if (request.Context?.VisibleProductIds?.Count > 20)
            return BadRequest(new { error = "Visible product hints exceed the allowed limit." });

        var customerId = CurrentUser.IsAuthenticated ? CurrentUser.UserId : null;
        var response = await _assistant.ChatAsync(request, customerId, ct);
        return Ok(response);
    }
}
