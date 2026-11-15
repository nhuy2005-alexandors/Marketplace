using ECommerce.Application.DTOs.Admin;
using ECommerce.Application.Common;
using ECommerce.Application.DTOs.Refunds;
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
    private readonly IRefundService _refunds;

    public AdminController(IDashboardService dashboard, ISellerAdminService sellers, IRefundService refunds)
    {
        _dashboard = dashboard;
        _sellers = sellers;
        _refunds = refunds;
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

    [HttpGet("refunds")]
    [ProducesResponseType(typeof(PagedResult<RefundDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<RefundDto>>> Refunds(
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => ToResponse(await _refunds.GetAllAsync(status, page, pageSize, ct));

    [HttpPost("refunds/{id:int}/complete")]
    [ProducesResponseType(typeof(RefundDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RefundDto>> CompleteRefund(
        int id, CompleteRefundRequest request, CancellationToken ct)
        => ToResponse(await _refunds.CompleteAsync(UserId, id, request, ct));

    [HttpPost("refunds/{id:int}/reject")]
    [ProducesResponseType(typeof(RefundDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RefundDto>> RejectRefund(
        int id, RejectRefundRequest request, CancellationToken ct)
        => ToResponse(await _refunds.RejectAsync(UserId, id, request, ct));

    [HttpPost("sellers/{id:int}/approve")]
    [ProducesResponseType(typeof(SellerApplicationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SellerApplicationDto>> ApproveSeller(int id, CancellationToken ct)
        => ToResponse(await _sellers.ApproveAsync(id, ct));
}
