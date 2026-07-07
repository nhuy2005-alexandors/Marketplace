# Coupon / Discount — Spec

> Spec hồi tố từ code hiện có.

## Mục tiêu
Cho phép áp mã giảm giá (percentage/fixed) vào subtotal trước khi checkout, với giới hạn số lần dùng và hạn dùng, chống over-redeem khi nhiều request cùng lúc.

## Yêu cầu
- [x] 2 loại giảm giá: `Percentage` (Value tính theo %) và `FixedAmount` (Value là số tiền cố định) (src/ECommerce.Domain/Entities/Coupon.cs:6-10)
- [x] Tạo coupon (Admin only): validate `Type` hợp lệ, `Value > 0`, Percentage không vượt 100, `Code` unique (uppercase, trim) (src/ECommerce.Application/Services/CouponService.cs:43-69)
- [x] Xóa coupon (Admin only), 404 nếu không tồn tại (src/ECommerce.Application/Services/CouponService.cs:71-79)
- [x] Liệt kê toàn bộ coupon cho Admin, sort theo `CreatedAt` giảm dần (src/ECommerce.Application/Services/CouponService.cs:25-29, src/ECommerce.API/Controllers/CouponsController.cs:28-34)
- [x] Endpoint public `GET /api/coupons/active` (AllowAnonymous, cho voucher strip trang chủ): chỉ trả coupon `IsActive`, chưa hết hạn (`ExpiresAt == null || ExpiresAt > now`) và chưa hết lượt (`MaxUses == null || TimesUsed < MaxUses`) (src/ECommerce.Application/Services/CouponService.cs:31-41, src/ECommerce.API/Controllers/CouponsController.cs:22-26)
- [x] Validate coupon trước checkout qua `POST /api/coupons/validate` (Authorize, không cần Admin): trả `CouponPreviewDto` (Code, DiscountAmount, NewTotal) nếu hợp lệ, 400 nếu invalid/expired (src/ECommerce.Application/Services/CouponService.cs:15-23, src/ECommerce.API/Controllers/CouponsController.cs:14-20)
- [x] Áp coupon thật khi checkout: tính lại discount trên subtotal đơn hàng, set `Order.CouponCode`/`Order.DiscountAmount`, gọi `coupon.Redeem()` tăng `TimesUsed` (src/ECommerce.Application/Services/OrderService.cs:65-75)
- [x] Kiểm tra hợp lệ dùng chung 1 hàm `IsValidFor` cho cả preview (validate) và checkout thật, tránh lệch logic (src/ECommerce.Domain/Entities/Coupon.cs:26-33; gọi tại CouponService.cs:19 và OrderService.cs:70)

## Ràng buộc
- `IsValidFor(subtotal, now)`: fail nếu `!IsActive`, hoặc `ExpiresAt < now`, hoặc `MaxUses.HasValue && TimesUsed >= MaxUses`, hoặc `orderSubtotal < MinOrderAmount` (src/ECommerce.Domain/Entities/Coupon.cs:26-33)
- `CalculateDiscount`: Percentage = `subtotal * Value / 100`, FixedAmount = `Value`; luôn `Math.Min(discount, orderSubtotal)` (không giảm quá subtotal); làm tròn 2 chữ số, `MidpointRounding.AwayFromZero` (src/ECommerce.Domain/Entities/Coupon.cs:35-41)
- `Order.Total = Max(0, Subtotal - DiscountAmount)` — chặn total âm ở tầng entity (src/ECommerce.Domain/Entities/Order.cs:22)
- `Code` là unique index ở DB, so sánh case-insensitive vì luôn `.ToUpperInvariant()` trước khi lưu/tra (src/ECommerce.Infrastructure/Persistence/Configurations/CouponConfiguration.cs:13; CouponService.cs:17,52)
- Chống over-redeem: `Coupon.RowVersion` là optimistic-concurrency token (`IsRowVersion()`), `SaveChangesAsync` trong `CheckoutAsync` bắt `DbUpdateConcurrencyException` → trả 409 Conflict thay vì tăng nhầm `TimesUsed` khi 2 request cùng redeem 1 coupon gần hết lượt (src/ECommerce.Infrastructure/Persistence/Configurations/CouponConfiguration.cs:17; src/ECommerce.Application/Services/OrderService.cs:80-88)
- `Value`, `MinOrderAmount` lưu `decimal(18,2)` ở DB (src/ECommerce.Infrastructure/Persistence/Configurations/CouponConfiguration.cs:15-16)
- `/api/coupons/validate` chỉ có `POST`, không có `GET` — route thật là `[HttpPost("validate")]` (src/ECommerce.API/Controllers/CouponsController.cs:15)

## Quyết định
- Validate (preview) và checkout dùng chung `IsValidFor`/`CalculateDiscount` trên entity — tránh lặp logic ở 2 tầng, nhưng preview không giữ lock nên vẫn có thể bị race (đã chặn ở bước checkout thật bằng RowVersion).
- Không có endpoint `Update` coupon (chỉ Create + Delete) — sửa coupon nghĩa là xóa/tạo lại.
- Discount không dùng bảng lịch sử redeem theo user — chỉ đếm tổng `TimesUsed`, không giới hạn "1 lần/user".

## Ngoài phạm vi
- Không giới hạn coupon theo user cụ thể (per-user usage cap) hay theo danh mục/sản phẩm.
- Không có coupon stacking (áp nhiều mã cùng lúc) — 1 order chỉ có 1 `CouponCode`.
- Không có cơ chế hoàn `TimesUsed` khi hủy đơn (`CancelAsync` không gọi lại giảm `TimesUsed` của coupon đã redeem).
- Không có Update/Edit coupon qua API.
