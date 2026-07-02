namespace ECommerce.Application.DTOs.Coupons;

public record CouponDto(
    int Id,
    string Code,
    string Type,
    decimal Value,
    decimal MinOrderAmount,
    DateTime? ExpiresAt,
    int? MaxUses,
    int TimesUsed,
    bool IsActive);

public record CreateCouponRequest(
    string Code,
    string Type,
    decimal Value,
    decimal MinOrderAmount,
    DateTime? ExpiresAt,
    int? MaxUses);

public record ValidateCouponRequest(string Code, decimal Subtotal);

public record CouponPreviewDto(string Code, decimal DiscountAmount, decimal NewTotal);
