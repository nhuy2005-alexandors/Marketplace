using ECommerce.Application.Common;
using ECommerce.Application.DTOs.Refunds;
using ECommerce.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.API.Controllers;

[Authorize(Roles = "Customer")]
public class RefundsController : ApiControllerBase
{
    private readonly IRefundService _refunds;

    public RefundsController(IRefundService refunds) => _refunds = refunds;

    [HttpPost]
    [ProducesResponseType(typeof(RefundDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RefundDto>> Request(RequestRefundRequest request, CancellationToken ct)
        => ToResponse(await _refunds.RequestAsync(UserId, request, ct));

    [HttpGet("mine")]
    [ProducesResponseType(typeof(PagedResult<RefundDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PagedResult<RefundDto>>> Mine(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100,
        CancellationToken ct = default)
        => ToResponse(await _refunds.GetForUserAsync(UserId, page, pageSize, ct));
}
