using ECommerce.Application.DTOs.Admin;
using ECommerce.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.API.Controllers;

[Authorize(Roles = "Admin")]
public sealed class RfmController(IRfmService rfm) : ApiControllerBase
{
    [HttpGet("customers")]
    [ProducesResponseType(typeof(RfmCustomerPageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<RfmCustomerPageDto>> Customers(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] DateTime? asOfUtc = null,
        CancellationToken cancellationToken = default) =>
        Ok(await rfm.GetCustomersAsync(
            page,
            pageSize,
            asOfUtc ?? DateTime.UtcNow,
            cancellationToken));
}
