namespace ECommerce.Application.DTOs.Orders;

public record OrderItemDto(int Id, int ProductId, string ProductName, decimal UnitPrice, int Quantity, decimal Subtotal, string Status);

public record PaymentDto(decimal Amount, string Method, string Status, string? TransactionId, DateTime? PaidAt);

public record OrderDto(
    int Id,
    string Status,
    string ShippingAddress,
    decimal Subtotal,
    decimal DiscountAmount,
    string? CouponCode,
    decimal Total,
    DateTime CreatedAt,
    IReadOnlyList<OrderItemDto> Items,
    PaymentDto? Payment);

public record CheckoutRequest(string ShippingAddress, string? CouponCode = null);

public record UpdateOrderStatusRequest(string Status);

public record UpdateFulfillmentStatusRequest(string Status);

// Method: mock | cod | vnpay | stripe. ReturnUrl: nơi cổng redirect về sau thanh toán.
public record PayOrderRequest(string Method, string? ReturnUrl);

// Kết quả khởi tạo thanh toán: hoặc hoàn tất ngay (Order), hoặc cần redirect.
public record PayResultDto(bool RequiresRedirect, string? RedirectUrl, OrderDto? Order);
