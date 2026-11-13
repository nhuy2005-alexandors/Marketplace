namespace ECommerce.Domain.Enums;

public enum OrderStatus
{
    Pending = 0,
    Paid = 1,
    Shipped = 2,
    Delivered = 3,
    Cancelled = 4,
    // COD order accepted for fulfillment; payment has not been collected.
    Accepted = 5
}
