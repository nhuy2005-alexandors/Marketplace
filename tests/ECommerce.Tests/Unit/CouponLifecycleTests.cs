using ECommerce.Domain.Entities;

namespace ECommerce.Tests.Unit;

public class CouponLifecycleTests
{
    [Fact]
    public void Coupon_is_expired_at_the_exact_expiry_instant()
    {
        var expiry = new DateTime(2026, 11, 14, 12, 0, 0, DateTimeKind.Utc);
        var coupon = new Coupon
        {
            Code = "EXACT",
            Type = DiscountType.Percentage,
            Value = 10,
            ExpiresAt = expiry
        };

        Assert.False(coupon.IsValidFor(100m, expiry));
        Assert.True(coupon.IsValidFor(100m, expiry.AddTicks(-1)));
    }
}
