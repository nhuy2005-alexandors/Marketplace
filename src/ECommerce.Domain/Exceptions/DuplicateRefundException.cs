namespace ECommerce.Domain.Exceptions;

public sealed class DuplicateRefundException(Exception inner)
    : Exception("A refund already exists for this order or payment.", inner);
