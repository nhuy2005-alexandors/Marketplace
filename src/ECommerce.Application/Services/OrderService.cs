using ECommerce.Application.Common;
using ECommerce.Application.DTOs.Orders;
using ECommerce.Application.Interfaces;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using ECommerce.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Application.Services;

public class OrderService : IOrderService
{
    private readonly IAppDbContext _db;

    public OrderService(IAppDbContext db) => _db = db;

    public async Task<Result<OrderDto>> CheckoutAsync(int userId, CheckoutRequest r, CancellationToken ct = default)
    {
        var cart = await _db.Carts
            .Include(c => c.Items).ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(c => c.UserId == userId, ct);

        if (cart is null || cart.Items.Count == 0)
            return Result.Fail<OrderDto>("Cart is empty.", ErrorType.Validation);

        var order = new Order
        {
            UserId = userId,
            Status = OrderStatus.Pending,
            ShippingAddress = r.ShippingAddress.Trim()
        };

        try
        {
            foreach (var ci in cart.Items)
            {
                ci.Product.DecreaseStock(ci.Quantity);
                order.Items.Add(new OrderItem
                {
                    ProductId = ci.ProductId,
                    ProductName = ci.Product.Name,
                    UnitPrice = ci.Product.Price,
                    Quantity = ci.Quantity
                });
            }
        }
        catch (DomainException ex)
        {
            return Result.Fail<OrderDto>(ex.Message, ErrorType.Validation);
        }

        if (!string.IsNullOrWhiteSpace(r.CouponCode))
        {
            var code = r.CouponCode.Trim().ToUpperInvariant();
            var coupon = await _db.Coupons.FirstOrDefaultAsync(c => c.Code == code, ct);
            var subtotal = order.Items.Sum(i => i.Subtotal);
            if (coupon is null || !coupon.IsValidFor(subtotal, DateTime.UtcNow))
                return Result.Fail<OrderDto>("Invalid or expired coupon.", ErrorType.Validation);
            order.CouponCode = coupon.Code;
            order.DiscountAmount = coupon.CalculateDiscount(subtotal);
            coupon.Redeem();
        }

        _db.Orders.Add(order);
        foreach (var ci in cart.Items.ToList())
            _db.CartItems.Remove(ci);
        await _db.SaveChangesAsync(ct);

        return Result.Ok(order.ToDto());
    }

    public async Task<PagedResult<OrderDto>> GetForUserAsync(int userId, int page, int pageSize, CancellationToken ct = default)
        => await PageAsync(BaseQuery().Where(o => o.UserId == userId), page, pageSize, ct);

    public async Task<PagedResult<OrderDto>> GetAllAsync(int page, int pageSize, CancellationToken ct = default)
        => await PageAsync(BaseQuery(), page, pageSize, ct);

    private static async Task<PagedResult<OrderDto>> PageAsync(IQueryable<Order> query, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(1, page);
        var size = Math.Clamp(pageSize, 1, 100);
        var total = await query.CountAsync(ct);
        var orders = await query
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(ct);
        return new PagedResult<OrderDto>
        {
            Items = orders.Select(o => o.ToDto()).ToList(),
            Page = page,
            PageSize = size,
            TotalCount = total
        };
    }

    public async Task<Result<OrderDto>> GetByIdAsync(int userId, bool isAdmin, int orderId, CancellationToken ct = default)
    {
        var order = await BaseQuery().FirstOrDefaultAsync(o => o.Id == orderId, ct);
        if (order is null)
            return Result.Fail<OrderDto>("Order not found.", ErrorType.NotFound);
        if (!isAdmin && order.UserId != userId)
            return Result.Fail<OrderDto>("Access denied.", ErrorType.Forbidden);
        return Result.Ok(order.ToDto());
    }

    public async Task<Result<OrderDto>> UpdateStatusAsync(int orderId, UpdateOrderStatusRequest r, CancellationToken ct = default)
    {
        if (!Enum.TryParse<OrderStatus>(r.Status, true, out var newStatus))
            return Result.Fail<OrderDto>("Invalid status.", ErrorType.Validation);

        var order = await BaseQuery().FirstOrDefaultAsync(o => o.Id == orderId, ct);
        if (order is null)
            return Result.Fail<OrderDto>("Order not found.", ErrorType.NotFound);

        try
        {
            order.ChangeStatus(newStatus);
        }
        catch (InvalidOrderTransitionException ex)
        {
            return Result.Fail<OrderDto>(ex.Message, ErrorType.Conflict);
        }
        await _db.SaveChangesAsync(ct);
        return Result.Ok(order.ToDto());
    }

    public async Task<Result<OrderDto>> CancelAsync(int userId, int orderId, CancellationToken ct = default)
    {
        var order = await BaseQuery().FirstOrDefaultAsync(o => o.Id == orderId, ct);
        if (order is null)
            return Result.Fail<OrderDto>("Order not found.", ErrorType.NotFound);
        if (order.UserId != userId)
            return Result.Fail<OrderDto>("Access denied.", ErrorType.Forbidden);
        if (!order.CanCancel())
            return Result.Fail<OrderDto>($"Cannot cancel an order in {order.Status} state.", ErrorType.Conflict);

        foreach (var item in order.Items)
        {
            var product = await _db.Products.FindAsync(new object[] { item.ProductId }, ct);
            if (product is not null)
                product.Stock += item.Quantity;
        }
        order.ChangeStatus(OrderStatus.Cancelled);
        await _db.SaveChangesAsync(ct);
        return Result.Ok(order.ToDto());
    }

    private IQueryable<Order> BaseQuery() =>
        _db.Orders.Include(o => o.Items).Include(o => o.Payment);
}
