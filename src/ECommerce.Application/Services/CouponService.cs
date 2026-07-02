using ECommerce.Application.Common;
using ECommerce.Application.DTOs.Coupons;
using ECommerce.Application.Interfaces;
using ECommerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Application.Services;

public class CouponService : ICouponService
{
    private readonly IAppDbContext _db;

    public CouponService(IAppDbContext db) => _db = db;

    public async Task<Result<CouponPreviewDto>> ValidateAsync(ValidateCouponRequest r, CancellationToken ct = default)
    {
        var code = r.Code.Trim().ToUpperInvariant();
        var coupon = await _db.Coupons.FirstOrDefaultAsync(c => c.Code == code, ct);
        if (coupon is null || !coupon.IsValidFor(r.Subtotal, DateTime.UtcNow))
            return Result.Fail<CouponPreviewDto>("Invalid or expired coupon.", ErrorType.Validation);
        var discount = coupon.CalculateDiscount(r.Subtotal);
        return Result.Ok(new CouponPreviewDto(coupon.Code, discount, Math.Max(0, r.Subtotal - discount)));
    }

    public async Task<IReadOnlyList<CouponDto>> GetAllAsync(CancellationToken ct = default)
    {
        var coupons = await _db.Coupons.OrderByDescending(c => c.CreatedAt).ToListAsync(ct);
        return coupons.Select(ToDto).ToList();
    }

    public async Task<Result<CouponDto>> CreateAsync(CreateCouponRequest r, CancellationToken ct = default)
    {
        if (!Enum.TryParse<DiscountType>(r.Type, true, out var type))
            return Result.Fail<CouponDto>("Invalid discount type.", ErrorType.Validation);
        if (r.Value <= 0)
            return Result.Fail<CouponDto>("Value must be positive.", ErrorType.Validation);
        if (type == DiscountType.Percentage && r.Value > 100)
            return Result.Fail<CouponDto>("Percentage cannot exceed 100.", ErrorType.Validation);

        var code = r.Code.Trim().ToUpperInvariant();
        if (await _db.Coupons.AnyAsync(c => c.Code == code, ct))
            return Result.Fail<CouponDto>("Coupon code already exists.", ErrorType.Conflict);

        var coupon = new Coupon
        {
            Code = code,
            Type = type,
            Value = r.Value,
            MinOrderAmount = r.MinOrderAmount,
            ExpiresAt = r.ExpiresAt,
            MaxUses = r.MaxUses,
            IsActive = true
        };
        _db.Coupons.Add(coupon);
        await _db.SaveChangesAsync(ct);
        return Result.Ok(ToDto(coupon));
    }

    public async Task<Result> DeleteAsync(int id, CancellationToken ct = default)
    {
        var coupon = await _db.Coupons.FindAsync(new object[] { id }, ct);
        if (coupon is null)
            return Result.Fail("Coupon not found.", ErrorType.NotFound);
        _db.Coupons.Remove(coupon);
        await _db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    private static CouponDto ToDto(Coupon c) =>
        new(c.Id, c.Code, c.Type.ToString(), c.Value, c.MinOrderAmount,
            c.ExpiresAt, c.MaxUses, c.TimesUsed, c.IsActive);
}
