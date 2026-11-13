using ECommerce.Domain.Common;
using ECommerce.Domain.Enums;

namespace ECommerce.Domain.Entities;

public class Payment : BaseEntity
{
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;

    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public string? TransactionId { get; set; }
    public DateTime? PaidAt { get; set; }
    public string? ProviderKey { get; set; }
    public string? ProviderReference { get; set; }
    public decimal? ProviderAmount { get; set; }
    public string? ProviderCurrency { get; set; }
    public bool RequiresReconciliation { get; set; }
    public bool IsDemo { get; set; }
    public decimal? ReconciliationAmount { get; set; }
    public string? ReconciliationCurrency { get; set; }
    public string? ReconciliationReference { get; set; }
    public byte[]? RowVersion { get; set; }
}
