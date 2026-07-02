using ECommerce.Application.Common;
using ECommerce.Application.DTOs.Orders;
using ECommerce.Application.Interfaces;
using ECommerce.Domain.Entities;
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
            .Include(o => o.Items)
            .Include(o => o.Payment)
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(ct);

        // Chỉ trả về item thuộc seller; tổng tính lại trên phần của seller.
        var dtos = orders.Select(o =>
        {
            var sellerItems = o.Items.Where(i => i.SellerId == sellerId).ToList();
            var subtotal = sellerItems.Sum(i => i.Subtotal);
            return new OrderDto(
                o.Id,
                o.Status.ToString(),
                o.ShippingAddress,
                subtotal,
                0m,
                o.CouponCode,
                subtotal,
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
}
