using ECommerce.Application.DTOs.Admin;
using ECommerce.Application.Interfaces;
using ECommerce.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Application.Services;

public class DashboardService : IDashboardService
{
    private readonly IAppDbContext _db;

    public DashboardService(IAppDbContext db) => _db = db;

    public async Task<DashboardDto> GetAsync(int? sellerId = null, CancellationToken ct = default)
    {
        var paidStatuses = new[] { OrderStatus.Paid, OrderStatus.Shipped, OrderStatus.Delivered };

        // Nạp đơn đã thanh toán kèm item để tính doanh thu SAU giảm giá coupon.
        var paidOrders = await _db.Orders
            .Where(o => paidStatuses.Contains(o.Status))
            .Include(o => o.Items)
            .ToListAsync(ct);

        var paidOrderIds = paidOrders.Select(o => o.Id).ToList();

        // Doanh thu (đã trừ coupon). System = tổng Order.Total; seller = phần seller sau khi chia tỉ lệ giảm giá.
        decimal revenue = 0m;
        foreach (var o in paidOrders)
        {
            if (sellerId == null)
            {
                revenue += o.Total;
            }
            else
            {
                var sellerSub = o.Items.Where(i => i.SellerId == sellerId.Value).Sum(i => i.Subtotal);
                if (sellerSub <= 0) continue;
                var orderSub = o.Subtotal;
                var sellerDiscount = orderSub > 0 ? Math.Round(o.DiscountAmount * (sellerSub / orderSub), 2) : 0m;
                revenue += Math.Max(0, sellerSub - sellerDiscount);
            }
        }

        // Số đơn có liên quan (seller: đơn chứa ít nhất 1 item của seller).
        var totalOrders = sellerId == null
            ? await _db.Orders.CountAsync(ct)
            : await _db.OrderItems.Where(oi => oi.SellerId == sellerId)
                .Select(oi => oi.OrderId).Distinct().CountAsync(ct);

        var totalProducts = sellerId == null
            ? await _db.Products.CountAsync(ct)
            : await _db.Products.CountAsync(p => p.SellerId == sellerId, ct);

        var totalCustomers = await _db.Users.CountAsync(u => u.Role == UserRole.Customer, ct);

        // Đơn theo trạng thái — seller: chỉ đơn chứa item của mình.
        var statusQuery = sellerId == null
            ? _db.Orders
            : _db.Orders.Where(o => o.Items.Any(i => i.SellerId == sellerId));
        var byStatus = await statusQuery
            .GroupBy(o => o.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        // Top sản phẩm bán chạy — từ item của các đơn đã thanh toán (seller: chỉ item của mình).
        var soldList = paidOrders
            .SelectMany(o => o.Items)
            .Where(i => sellerId == null || i.SellerId == sellerId.Value)
            .ToList();

        var topProducts = soldList
            .GroupBy(oi => new { oi.ProductId, oi.ProductName })
            .Select(g => new TopProductDto(
                g.Key.ProductId,
                g.Key.ProductName,
                g.Sum(x => x.Quantity),
                g.Sum(x => x.UnitPrice * x.Quantity)))
            .OrderByDescending(x => x.UnitsSold)
            .Take(5)
            .ToList();

        return new DashboardDto(
            revenue,
            totalOrders,
            totalProducts,
            totalCustomers,
            byStatus.Select(s => new StatusCountDto(s.Status.ToString(), s.Count)).ToList(),
            topProducts);
    }
}
