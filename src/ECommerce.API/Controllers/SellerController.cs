using ECommerce.Application.Common;
using ECommerce.Application.DTOs.Admin;
using ECommerce.Application.DTOs.Orders;
using ECommerce.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.API.Controllers;

[Authorize(Roles = "Seller")]
[Route("api/seller")]
public class SellerController : ApiControllerBase
{
    private readonly IDashboardService _dashboard;
    private readonly ISellerOrderService _orders;

    public SellerController(IDashboardService dashboard, ISellerOrderService orders)
    {
        _dashboard = dashboard;
        _orders = orders;
    }

    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(DashboardDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<DashboardDto>> Dashboard(CancellationToken ct)
        => Ok(await _dashboard.GetAsync(UserId, ct));

    [HttpGet("orders")]
    [ProducesResponseType(typeof(PagedResult<OrderDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResult<OrderDto>>> Orders(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken ct = default)
        => Ok(await _orders.GetForSellerAsync(UserId, page, pageSize, ct));

    // Seller cập nhật trạng thái giao hàng cho một item của mình.
    [HttpPut("orders/items/{itemId:int}/status")]
    [ProducesResponseType(typeof(OrderItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrderItemDto>> UpdateItemStatus(int itemId, UpdateFulfillmentStatusRequest request, CancellationToken ct)
        => ToResponse(await _orders.UpdateFulfillmentAsync(UserId, itemId, request, ct));
}
