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

        var paidOrderIds = await _db.Orders
            .Where(o => paidStatuses.Contains(o.Status))
            .Select(o => o.Id)
            .ToListAsync(ct);

        // Item bán được (đơn đã thanh toán); nếu seller -> chỉ item của seller.
        var soldItems = _db.OrderItems
            .Where(oi => paidOrderIds.Contains(oi.OrderId));

        if (sellerId != null)
            soldItems = soldItems.Where(oi => oi.SellerId == sellerId.Value);

        var revenue = await soldItems.SumAsync(x => (decimal?)(x.UnitPrice * x.Quantity), ct) ?? 0m;

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

        var soldList = await soldItems
            .Select(oi => new { oi.ProductId, oi.ProductName, oi.Quantity, oi.UnitPrice })
            .ToListAsync(ct);

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
