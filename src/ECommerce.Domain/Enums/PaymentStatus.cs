namespace ECommerce.Domain.Enums;

public enum PaymentStatus
{
    Pending = 0,
    Completed = 1,
    Failed = 2,
    Refunded = 3
}

public enum PaymentMethod
{
    CreditCard = 0,
    PayPal = 1,
    CashOnDelivery = 2
}
