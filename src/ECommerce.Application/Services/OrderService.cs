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
    private readonly IEmailSender _email;

    public OrderService(IAppDbContext db, IEmailSender? email = null)
    {
        _db = db;
        _email = email ?? new NullEmailSender();
    }

    // No-op fallback giữ ctor cũ `new OrderService(db)` compile được cho unit test hiện có.
    private sealed class NullEmailSender : IEmailSender
    {
        public Task SendAsync(string toEmail, string subject, string body, CancellationToken ct = default)
            => Task.CompletedTask;
    }

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
                    SellerId = ci.Product.SellerId,
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
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Xung đột optimistic lock: có request khác vừa đổi tồn kho/lượt coupon. Chặn oversell/over-redeem.
            return Result.Fail<OrderDto>("Sản phẩm hoặc mã giảm giá vừa được cập nhật, vui lòng thử lại.", ErrorType.Conflict);
        }

        try
        {
            var email = await _db.Users
                .Where(u => u.Id == userId)
                .Select(u => u.Email)
                .FirstOrDefaultAsync(ct);
            if (!string.IsNullOrWhiteSpace(email))
            {
                await _email.SendAsync(
                    email,
                    $"Xác nhận đơn hàng #{order.Id}",
                    $"Đơn hàng #{order.Id} của bạn đã được đặt thành công. Tổng tiền: {order.Total:N0}đ.",
                    ct);
            }
        }
        catch (Exception)
        {
            // Best-effort: gửi email thất bại không được làm fail checkout.
        }

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

        var productIds = order.Items.Select(i => i.ProductId).ToList();
        var products = await _db.Products.Where(p => productIds.Contains(p.Id)).ToListAsync(ct);
        var byId = products.ToDictionary(p => p.Id);
        foreach (var item in order.Items)
        {
            if (byId.TryGetValue(item.ProductId, out var product))
                product.Stock += item.Quantity;
            if (item.Status == Domain.Enums.FulfillmentStatus.Pending)
                item.Status = Domain.Enums.FulfillmentStatus.Cancelled;
        }
        order.ChangeStatus(OrderStatus.Cancelled);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Cancel song song / tồn kho vừa đổi (Product RowVersion): trả Conflict thay vì 500.
            return Result.Fail<OrderDto>("Đơn hàng vừa được cập nhật, vui lòng thử lại.", ErrorType.Conflict);
        }
        return Result.Ok(order.ToDto());
    }

    public async Task<Result<OrderSplitDto>> GetSplitAsync(int userId, bool isAdmin, int orderId, CancellationToken ct = default)
    {
        var order = await _db.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId, ct);
        if (order is null)
            return Result.Fail<OrderSplitDto>("Order not found.", ErrorType.NotFound);
        if (!isAdmin && order.UserId != userId)
            return Result.Fail<OrderSplitDto>("Access denied.", ErrorType.Forbidden);

        var sellerIds = order.Items.Select(i => i.SellerId).Distinct().ToList();
        var shopNames = await _db.Users
            .AsNoTracking()
            .Where(u => sellerIds.Contains(u.Id))
            .Select(u => new { u.Id, Name = u.ShopName ?? u.FullName })
            .ToDictionaryAsync(u => u.Id, u => u.Name, ct);

        var orderSubtotal = order.Subtotal;
        var groups = order.Items
            .GroupBy(i => i.SellerId)
            .OrderBy(g => g.Key)
            .ToList();

        var splits = new List<SellerSplitDto>(groups.Count);
        var allocated = 0m;
        for (var idx = 0; idx < groups.Count; idx++)
        {
            var g = groups[idx];
            var sellerSubtotal = g.Sum(i => i.Subtotal);
            // Pro-rate coupon discount by seller subtotal share; last seller absorbs the
            // rounding remainder so the per-seller shares sum back to the order discount exactly.
            decimal discountShare;
            if (idx == groups.Count - 1)
                // Clamp [0, sellerSubtotal] để phần dư làm tròn không tạo giảm giá âm/vượt subtotal.
                discountShare = Math.Clamp(order.DiscountAmount - allocated, 0m, sellerSubtotal);
            else
            {
                discountShare = orderSubtotal > 0
                    ? Math.Round(order.DiscountAmount * (sellerSubtotal / orderSubtotal), 2)
                    : 0m;
                allocated += discountShare;
            }
            splits.Add(new SellerSplitDto(
                g.Key,
                shopNames.TryGetValue(g.Key, out var n) ? n : $"Seller #{g.Key}",
                sellerSubtotal,
                discountShare,
                Math.Max(0, sellerSubtotal - discountShare),
                g.Select(i => i.ToDto()).ToList()));
        }

        return Result.Ok(new OrderSplitDto(
            order.Id, orderSubtotal, order.DiscountAmount, order.Total, splits));
    }

    private IQueryable<Order> BaseQuery() =>
        _db.Orders.Include(o => o.Items).Include(o => o.Payment);
}
