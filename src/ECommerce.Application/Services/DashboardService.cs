using ECommerce.Application.DTOs.Admin;
using ECommerce.Application.Interfaces;
using ECommerce.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Application.Services;

public class DashboardService : IDashboardService
{
    private readonly IAppDbContext _db;

    public DashboardService(IAppDbContext db) => _db = db;

    public async Task<DashboardDto> GetAsync(CancellationToken ct = default)
    {
        var paidStatuses = new[] { OrderStatus.Paid, OrderStatus.Shipped, OrderStatus.Delivered };

        var revenue = await _db.Payments
            .Where(p => p.Status == PaymentStatus.Completed)
            .SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;

        var totalOrders = await _db.Orders.CountAsync(ct);
        var totalProducts = await _db.Products.CountAsync(ct);
        var totalCustomers = await _db.Users.CountAsync(u => u.Role == UserRole.Customer, ct);

        var byStatus = await _db.Orders
            .GroupBy(o => o.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var topProducts = await (
            from oi in _db.OrderItems
            join o in _db.Orders on oi.OrderId equals o.Id
            where paidStatuses.Contains(o.Status)
            group oi by new { oi.ProductId, oi.ProductName } into g
            orderby g.Sum(x => x.Quantity) descending
            select new TopProductDto(
                g.Key.ProductId,
                g.Key.ProductName,
                g.Sum(x => x.Quantity),
                g.Sum(x => x.UnitPrice * x.Quantity)))
            .Take(5)
            .ToListAsync(ct);

        return new DashboardDto(
            revenue,
            totalOrders,
            totalProducts,
            totalCustomers,
            byStatus.Select(s => new StatusCountDto(s.Status.ToString(), s.Count)).ToList(),
            topProducts);
    }
}
