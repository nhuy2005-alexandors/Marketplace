using ECommerce.Application.Interfaces;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;

namespace ECommerce.Application.Services;

internal static class OrderLifecycle
{
    // Every competing lifecycle writer updates this same parent row. SQL Server
    // validates the original Order.RowVersion in the atomic SaveChanges transaction.
    internal static void Touch(IAppDbContext db, Order order)
    {
        order.UpdatedAt = DateTime.UtcNow;
        db.Entry(order).Property(o => o.UpdatedAt).IsModified = true;
    }

    internal static bool CanFulfill(Order order) => !order.RefundPending && order.Payment?.RequiresReconciliation != true &&
        ((order.Status is OrderStatus.Paid or OrderStatus.Shipped &&
          order.Payment?.Status == PaymentStatus.Completed) ||
         (order.Status is OrderStatus.Accepted or OrderStatus.Shipped &&
          order.Payment?.Method == PaymentMethod.CashOnDelivery && order.Payment.Status == PaymentStatus.Pending));
}
