using ECommerce.Domain.Common;
using ECommerce.Domain.Exceptions;

namespace ECommerce.Domain.Entities;

public enum DiscountType
{
    Percentage = 0,
    FixedAmount = 1
}

public class Coupon : BaseEntity
{
    public string Code { get; set; } = null!;
    public DiscountType Type { get; set; }
    public decimal Value { get; set; }
    public decimal MinOrderAmount { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public int? MaxUses { get; set; }
    public int TimesUsed { get; set; }
    public bool IsActive { get; set; } = true;

    // Optimistic concurrency token — chặn over-redeem khi nhiều request cùng lúc.
    public byte[]? RowVersion { get; set; }

    public bool IsValidFor(decimal orderSubtotal, DateTime now)
    {
        if (!IsActive) return false;
        if (ExpiresAt.HasValue && ExpiresAt.Value < now) return false;
        if (MaxUses.HasValue && TimesUsed >= MaxUses.Value) return false;
        if (orderSubtotal < MinOrderAmount) return false;
        return true;
    }

    public decimal CalculateDiscount(decimal orderSubtotal)
    {
        var discount = Type == DiscountType.Percentage
            ? orderSubtotal * Value / 100m
            : Value;
        return Math.Round(Math.Min(discount, orderSubtotal), 2, MidpointRounding.AwayFromZero);
    }

    public void Redeem()
    {
        TimesUsed++;
    }
}
