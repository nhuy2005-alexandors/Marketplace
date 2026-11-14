using ECommerce.Domain.Common;
using ECommerce.Domain.Enums;

namespace ECommerce.Domain.Entities;

public class Payment : BaseEntity
{
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;

    // Canonical order/catalog amount in USD. Provider-native amounts are stored separately below.
    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public string? TransactionId { get; set; }
    public DateTime? PaidAt { get; set; }
    public string? ProviderKey { get; set; }
    public string? ProviderReference { get; set; }
    // Frozen provider quote/capture in provider currency (MoMo: VND); never overwrite Amount.
    public decimal? ProviderAmount { get; set; }
    public string? ProviderCurrency { get; set; }
    public bool RequiresReconciliation { get; set; }
    public bool IsDemo { get; set; }
    // Observed provider-native amount/currency when a capture needs reconciliation.
    public decimal? ReconciliationAmount { get; set; }
    public string? ReconciliationCurrency { get; set; }
    public string? ReconciliationReference { get; set; }
    public byte[]? RowVersion { get; set; }
}
