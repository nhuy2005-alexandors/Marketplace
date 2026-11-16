using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;

namespace ECommerce.Application.Services;

internal static class PurchaseEligibility
{
    public static IQueryable<Order> EligiblePurchases(this IQueryable<Order> orders, DateTime? cutoff = null)
    {
        var query = orders.Where(order =>
            (order.Status == OrderStatus.Paid || order.Status == OrderStatus.Shipped || order.Status == OrderStatus.Delivered) &&
            order.Payment != null && order.Payment.Status == PaymentStatus.Completed &&
            !order.Payment.IsDemo && !order.Payment.RequiresReconciliation && !order.RefundPending &&
            order.Items.Any(item => item.Status != FulfillmentStatus.Cancelled));
        if (cutoff is DateTime end)
            query = query.Where(order => order.CreatedAt < end && order.Payment!.CreatedAt < end &&
                (order.Payment.PaidAt ?? order.Payment.CreatedAt) < end);
        return query;
    }
}
