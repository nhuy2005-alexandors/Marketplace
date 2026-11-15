using ECommerce.Application.DTOs.AI;
using ECommerce.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.API.Controllers;

[AllowAnonymous]
public sealed class AssistantController(IAssistantService assistant) : ApiControllerBase
{
    [HttpPost("chat")]
    [ProducesResponseType(typeof(AssistantChatResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AssistantChatResponse>> Chat(
        [FromBody] AssistantChatRequest request,
        CancellationToken cancellationToken)
    {
        var customerId = User.Identity?.IsAuthenticated == true && User.IsInRole("Customer")
            ? UserId
            : null;

        var response = await assistant.ChatAsync(request, customerId, cancellationToken);
        return Ok(response);
    }
}
