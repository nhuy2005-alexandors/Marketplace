using ECommerce.Domain.Common;
using ECommerce.Domain.Enums;
using ECommerce.Domain.Exceptions;

namespace ECommerce.Domain.Entities;

public class OrderItem : BaseEntity
{
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;

    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public int SellerId { get; set; }

    public string ProductName { get; set; } = null!;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }

    public FulfillmentStatus Status { get; set; } = FulfillmentStatus.Pending;

    public decimal Subtotal => UnitPrice * Quantity;

    private static readonly Dictionary<FulfillmentStatus, FulfillmentStatus[]> AllowedTransitions = new()
    {
        [FulfillmentStatus.Pending] = new[] { FulfillmentStatus.Shipped, FulfillmentStatus.Cancelled },
        [FulfillmentStatus.Shipped] = new[] { FulfillmentStatus.Delivered },
        [FulfillmentStatus.Delivered] = Array.Empty<FulfillmentStatus>(),
        [FulfillmentStatus.Cancelled] = Array.Empty<FulfillmentStatus>()
    };

    public void ChangeStatus(FulfillmentStatus newStatus)
    {
        if (Status == newStatus)
            return;
        if (!AllowedTransitions.TryGetValue(Status, out var allowed) || !allowed.Contains(newStatus))
            throw new InvalidOrderTransitionException(
                $"Cannot transition order item from {Status} to {newStatus}.");
        Status = newStatus;
        UpdatedAt = DateTime.UtcNow;
    }
}

