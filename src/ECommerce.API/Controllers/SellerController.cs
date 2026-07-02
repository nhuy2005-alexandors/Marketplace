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
    public async Task<ActionResult<DashboardDto>> Dashboard(CancellationToken ct)
        => Ok(await _dashboard.GetAsync(UserId, ct));

    [HttpGet("orders")]
    public async Task<ActionResult<PagedResult<OrderDto>>> Orders(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken ct = default)
        => Ok(await _orders.GetForSellerAsync(UserId, page, pageSize, ct));
}
