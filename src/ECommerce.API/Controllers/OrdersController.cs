using ECommerce.Application.Common;
using ECommerce.Application.DTOs.Orders;
using ECommerce.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.API.Controllers;

[Authorize]
public class OrdersController : ApiControllerBase
{
    private readonly IOrderService _orders;
    private readonly IPaymentService _payments;

    public OrdersController(IOrderService orders, IPaymentService payments)
    {
        _orders = orders;
        _payments = payments;
    }

    [HttpPost]
    public async Task<ActionResult<OrderDto>> Checkout(CheckoutRequest request, CancellationToken ct)
        => ToResponse(await _orders.CheckoutAsync(UserId, request, ct));

    [HttpGet]
    public async Task<ActionResult<PagedResult<OrderDto>>> Get([FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken ct = default)
        => Ok(IsAdmin
            ? await _orders.GetAllAsync(page, pageSize, ct)
            : await _orders.GetForUserAsync(UserId, page, pageSize, ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<OrderDto>> GetById(int id, CancellationToken ct)
        => ToResponse(await _orders.GetByIdAsync(UserId, IsAdmin, id, ct));

    // Khởi tạo thanh toán: trả redirect URL (vnpay/stripe) hoặc Order hoàn tất (mock/cod).
    [HttpPost("{id:int}/pay")]
    public async Task<ActionResult<PayResultDto>> Pay(int id, PayOrderRequest request, CancellationToken ct)
        => ToResponse(await _payments.InitiateAsync(UserId, id, request, ct));

    [HttpPost("{id:int}/cancel")]
    public async Task<ActionResult<OrderDto>> Cancel(int id, CancellationToken ct)
        => ToResponse(await _orders.CancelAsync(UserId, id, ct));

    [Authorize(Roles = "Admin")]
    [HttpPut("{id:int}/status")]
    public async Task<ActionResult<OrderDto>> UpdateStatus(int id, UpdateOrderStatusRequest request, CancellationToken ct)
        => ToResponse(await _orders.UpdateStatusAsync(id, request, ct));
}
