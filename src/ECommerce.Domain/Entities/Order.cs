using ECommerce.Domain.Common;
using ECommerce.Domain.Enums;
using ECommerce.Domain.Exceptions;

namespace ECommerce.Domain.Entities;

public class Order : BaseEntity
{
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public string ShippingAddress { get; set; } = null!;

    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
    public Payment? Payment { get; set; }

    public string? CouponCode { get; set; }
    public decimal DiscountAmount { get; set; }

    public decimal Subtotal => Items.Sum(i => i.Subtotal);
    public decimal Total => Math.Max(0, Subtotal - DiscountAmount);

    private static readonly Dictionary<OrderStatus, OrderStatus[]> AllowedTransitions = new()
    {
        [OrderStatus.Pending] = new[] { OrderStatus.Paid, OrderStatus.Cancelled },
        [OrderStatus.Paid] = new[] { OrderStatus.Shipped, OrderStatus.Cancelled },
        [OrderStatus.Shipped] = new[] { OrderStatus.Delivered },
        [OrderStatus.Delivered] = Array.Empty<OrderStatus>(),
        [OrderStatus.Cancelled] = Array.Empty<OrderStatus>()
    };

    public void ChangeStatus(OrderStatus newStatus)
    {
        if (Status == newStatus)
            return;
        if (!AllowedTransitions.TryGetValue(Status, out var allowed) || !allowed.Contains(newStatus))
            throw new InvalidOrderTransitionException(
                $"Cannot transition order from {Status} to {newStatus}.");
        Status = newStatus;
        UpdatedAt = DateTime.UtcNow;
    }

    public bool CanCancel() =>
        AllowedTransitions.TryGetValue(Status, out var allowed) && allowed.Contains(OrderStatus.Cancelled);
}
