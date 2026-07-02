using ECommerce.Application.Common;
using ECommerce.Application.DTOs.Orders;
using ECommerce.Application.Interfaces;
using ECommerce.Domain.Enums;
using ECommerce.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Application.Services;

public class SellerOrderService : ISellerOrderService
{
    private readonly IAppDbContext _db;

    public SellerOrderService(IAppDbContext db) => _db = db;

    public async Task<PagedResult<OrderDto>> GetForSellerAsync(int sellerId, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        var size = Math.Clamp(pageSize, 1, 100);

        var baseQuery = _db.Orders
            .Where(o => o.Items.Any(i => i.SellerId == sellerId));

        var total = await baseQuery.CountAsync(ct);

        var orders = await baseQuery
            .AsNoTracking()
            .Include(o => o.Items)
            .Include(o => o.Payment)
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(ct);

        // Chỉ trả về item thuộc seller; giảm giá coupon toàn đơn được chia tỉ lệ theo phần của seller.
        var dtos = orders.Select(o =>
        {
            var sellerItems = o.Items.Where(i => i.SellerId == sellerId).ToList();
            var sellerSubtotal = sellerItems.Sum(i => i.Subtotal);

            // Pro-rate: phần giảm giá của seller = tỉ trọng subtotal seller trong tổng đơn.
            var orderSubtotal = o.Subtotal;
            var sellerDiscount = orderSubtotal > 0
                ? Math.Round(o.DiscountAmount * (sellerSubtotal / orderSubtotal), 2)
                : 0m;
            var sellerTotal = Math.Max(0, sellerSubtotal - sellerDiscount);

            return new OrderDto(
                o.Id,
                o.Status.ToString(),
                o.ShippingAddress,
                sellerSubtotal,
                sellerDiscount,
                o.CouponCode,
                sellerTotal,
                o.CreatedAt,
                sellerItems.Select(i => i.ToDto()).ToList(),
                o.Payment?.ToDto());
        }).ToList();

        return new PagedResult<OrderDto>
        {
            Items = dtos,
            Page = page,
            PageSize = size,
            TotalCount = total
        };
    }

    public async Task<Result<OrderItemDto>> UpdateFulfillmentAsync(int sellerId, int orderItemId, UpdateFulfillmentStatusRequest r, CancellationToken ct = default)
    {
        if (!Enum.TryParse<FulfillmentStatus>(r.Status, true, out var newStatus))
            return Result.Fail<OrderItemDto>("Invalid fulfillment status.", ErrorType.Validation);

        var item = await _db.OrderItems.FirstOrDefaultAsync(i => i.Id == orderItemId, ct);
        if (item is null)
            return Result.Fail<OrderItemDto>("Order item not found.", ErrorType.NotFound);
        if (item.SellerId != sellerId)
            return Result.Fail<OrderItemDto>("You can only fulfill your own items.", ErrorType.Forbidden);

        try
        {
            item.ChangeStatus(newStatus);
        }
        catch (InvalidOrderTransitionException ex)
        {
            return Result.Fail<OrderItemDto>(ex.Message, ErrorType.Conflict);
        }
        await _db.SaveChangesAsync(ct);
        return Result.Ok(item.ToDto());
    }
}
