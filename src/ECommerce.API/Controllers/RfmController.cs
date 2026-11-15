using ECommerce.Application.DTOs.Admin;
using ECommerce.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.API.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin")]
public sealed class RfmController : ControllerBase
{
    private readonly IRfmService _rfm;

    public RfmController(IRfmService rfm) => _rfm = rfm;

    [HttpGet("customers/rfm")]
    [ProducesResponseType(typeof(RfmCustomerPageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<RfmCustomerPageDto>> Customers(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        // Capture one clock value for this request and use it for both the metric
        // reference and the exclusive data cutoff.
        var asOfUtc = DateTime.UtcNow;
        var result = await _rfm.GetCustomersAsync(
            page,
            pageSize,
            asOfUtc,
            cutoffExclusiveUtc: asOfUtc,
            cancellationToken);
        return Ok(result);
    }
}
