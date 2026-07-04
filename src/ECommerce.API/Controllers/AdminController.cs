using ECommerce.Application.DTOs.Admin;
using ECommerce.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.API.Controllers;

[Authorize(Roles = "Admin")]
[Route("api/admin")]
public class AdminController : ApiControllerBase
{
    private readonly IDashboardService _dashboard;
    private readonly ISellerAdminService _sellers;

    public AdminController(IDashboardService dashboard, ISellerAdminService sellers)
    {
        _dashboard = dashboard;
        _sellers = sellers;
    }

    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(DashboardDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<DashboardDto>> Dashboard(CancellationToken ct)
        => Ok(await _dashboard.GetAsync(null, ct));

    // Danh sách seller để duyệt; ?status=Pending|Approved để lọc.
    [HttpGet("sellers")]
    [ProducesResponseType(typeof(IReadOnlyList<SellerApplicationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<SellerApplicationDto>>> Sellers([FromQuery] string? status, CancellationToken ct)
        => Ok(await _sellers.GetSellersAsync(status, ct));

    [HttpPost("sellers/{id:int}/approve")]
    [ProducesResponseType(typeof(SellerApplicationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SellerApplicationDto>> ApproveSeller(int id, CancellationToken ct)
        => ToResponse(await _sellers.ApproveAsync(id, ct));
}
