using ECommerce.Domain.Common;
using ECommerce.Domain.Enums;
using ECommerce.Domain.Exceptions;

namespace ECommerce.Domain.Entities;

public class Refund : BaseEntity
{
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;

    public int PaymentId { get; set; }
    public Payment Payment { get; set; } = null!;

    public decimal Amount { get; set; }
    public string Reason { get; set; } = null!;
    public RefundStatus Status { get; set; } = RefundStatus.Pending;

    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }
    public int? ProcessedBy { get; set; }
    public User? ProcessedByUser { get; set; }

    public string? ExternalReference { get; set; }
    public string? Note { get; set; }

    public void Complete(int adminId, string externalReference, string? note)
    {
        EnsurePending();
        if (adminId <= 0)
            throw new InvalidRefundTransitionException("A valid admin is required to complete a refund.");
        if (string.IsNullOrWhiteSpace(externalReference))
            throw new InvalidRefundTransitionException("An external refund reference is required.");

        Status = RefundStatus.Completed;
        ProcessedAt = DateTime.UtcNow;
        ProcessedBy = adminId;
        ExternalReference = externalReference.Trim();
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        UpdatedAt = ProcessedAt;
    }

    public void Reject(int adminId, string reason)
    {
        EnsurePending();
        if (adminId <= 0)
            throw new InvalidRefundTransitionException("A valid admin is required to reject a refund.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidRefundTransitionException("A rejection reason is required.");

        Status = RefundStatus.Rejected;
        ProcessedAt = DateTime.UtcNow;
        ProcessedBy = adminId;
        Note = reason.Trim();
        UpdatedAt = ProcessedAt;
    }

    private void EnsurePending()
    {
        if (Status != RefundStatus.Pending)
            throw new InvalidRefundTransitionException($"Cannot process a refund in {Status} state.");
    }
}
