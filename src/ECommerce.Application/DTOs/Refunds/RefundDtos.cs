namespace ECommerce.Application.DTOs.Refunds;

public record RequestRefundRequest(int OrderId, string Reason);

public record CompleteRefundRequest(string ExternalReference, string? Note = null);

public record RejectRefundRequest(string Reason);

public record RefundDto(
    int Id,
    int OrderId,
    string CustomerName,
    decimal Amount,
    string Reason,
    string Status,
    DateTime RequestedAt,
    DateTime? ProcessedAt,
    string? ExternalReference,
    string? Note,
    string? ProcessedByName);
