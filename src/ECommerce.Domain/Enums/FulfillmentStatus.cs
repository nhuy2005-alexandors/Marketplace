namespace ECommerce.Domain.Enums;

// Trạng thái giao hàng ở cấp từng OrderItem — mỗi seller tự cập nhật phần của mình.
public enum FulfillmentStatus
{
    Pending = 0,
    Shipped = 1,
    Delivered = 2,
    Cancelled = 3
}
