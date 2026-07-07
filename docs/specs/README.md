# Specs (đặc tả trước khi code)

Mỗi feature → 1 file `<tên>.md` mô tả **sẽ làm gì** (khác `../tech_specs/` — ghi **đã làm thế nào** sau khi code).

> Các spec dưới đây được viết **hồi tố** (reverse-documented) từ code hiện có để chuẩn hóa bộ tài liệu — mô tả yêu cầu/ràng buộc/quyết định của từng feature như thể đặc tả trước khi build.

## Template
- **Mục tiêu**: feature làm gì (1-2 câu).
- **Yêu cầu**: checklist `[ ]` điều kiện phải đạt.
- **Ràng buộc**: giới hạn kỹ thuật/bảo mật.
- **Quyết định**: chọn gì, vì sao.
- **Ngoài phạm vi**: cái KHÔNG làm.

## Index
| Spec | Phạm vi |
|------|---------|
| [auth-rbac.md](auth-rbac.md) | Đăng ký/đăng nhập, JWT, refresh token, phân quyền RBAC |
| [product-catalog.md](product-catalog.md) | Tìm/lọc/phân trang sản phẩm, CRUD sản phẩm + danh mục, upload ảnh |
| [cart-order.md](cart-order.md) | Giỏ hàng, checkout, vòng đời đơn, hủy đơn, chia tiền theo seller |
| [payment.md](payment.md) | Thanh toán đa cổng (Mock/COD/MoMo) |
| [coupon.md](coupon.md) | Mã giảm giá (percentage/fixed, hạn dùng, giới hạn lượt) |
| [marketplace-seller.md](marketplace-seller.md) | Onboarding seller, duyệt, giao hàng theo item |
| [engagement.md](engagement.md) | Đánh giá (verified-purchase), wishlist, dashboard |
</content>
