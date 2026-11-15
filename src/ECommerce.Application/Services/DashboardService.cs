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
        var paidOrders = await _db.Orders
            .Where(o => paidStatuses.Contains(o.Status) && o.Payment != null &&
                o.Payment.Status == PaymentStatus.Completed && !o.Payment.RequiresReconciliation && !o.Payment.IsDemo)
            .Include(o => o.Items)
            .ToListAsync(ct);

        decimal revenue = 0m;
        foreach (var order in paidOrders)
        {
            var originalSubtotal = order.Items.Sum(i => i.Subtotal);
            var activeItems = order.Items.Where(i => i.Status != FulfillmentStatus.Cancelled).ToList();
            var activeSubtotal = activeItems.Sum(i => i.Subtotal);
            if (activeSubtotal <= 0 || originalSubtotal <= 0) continue;

            var sellerSubtotal = sellerId.HasValue
                ? activeItems.Where(i => i.SellerId == sellerId.Value).Sum(i => i.Subtotal)
                : activeSubtotal;
            if (sellerSubtotal <= 0) continue;

            var allocatedDiscount = Math.Round(
                order.DiscountAmount * (sellerSubtotal / originalSubtotal), 2, MidpointRounding.AwayFromZero);
            revenue += Math.Max(0m, sellerSubtotal - allocatedDiscount);
        }

        var totalOrders = sellerId == null
            ? await _db.Orders.CountAsync(ct)
            : await _db.OrderItems.Where(oi => oi.SellerId == sellerId)
                .Select(oi => oi.OrderId).Distinct().CountAsync(ct);

        var totalProducts = sellerId == null
            ? await _db.Products.CountAsync(ct)
            : await _db.Products.CountAsync(p => p.SellerId == sellerId, ct);

        var totalCustomers = sellerId == null
            ? await _db.Users.CountAsync(u => u.Role == UserRole.Customer, ct)
            : await _db.Orders.Where(o => o.Items.Any(i => i.SellerId == sellerId))
                .Select(o => o.UserId).Distinct().CountAsync(ct);
        var statusQuery = sellerId == null
            ? _db.Orders
            : _db.Orders.Where(o => o.Items.Any(i => i.SellerId == sellerId));
        var byStatus = await statusQuery
            .GroupBy(o => o.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var soldList = paidOrders
            .SelectMany(o => o.Items)
            .Where(i => i.Status != FulfillmentStatus.Cancelled &&
                        (sellerId == null || i.SellerId == sellerId.Value))
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
