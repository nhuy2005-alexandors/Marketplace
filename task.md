# Task Checkpoint — MiniShop

Cập nhật: 2026-07-05

## Mẻ hiện tại (2026-07-05, CHƯA COMMIT) — `docs/tech_specs/2026-07-05-marketplace-ui-momo-docs.md`

| # | Task | Trạng thái | Ghi chú |
|---|------|-----------|---------|
| 1 | UI marketplace revamp | ✅ | Palette xanh, header 2 tầng, HomePage (hero/voucher/flash-sale/top-rated/low-stock), Footer link thật, emoji→lucide, max-w-7xl, grid 5 cột |
| 2 | Thanh toán MoMo cổng thật | ✅ | `MoMoProvider` AIO v2 sandbox (HMAC-SHA256, payWithMethod, USD→VND), callback + IPN. Gỡ sạch Stripe + VNPay |
| 3 | Backend hỗ trợ homepage | ✅ | `GET /coupons/active` (public), sort `rating`+`stock` cho ProductService |
| 4 | Gom diagram vào `docs/diagrams/` | ✅ | git mv 6 sơ đồ; sync cross-ref (SRS/DECISIONS/README) |
| 5 | Thêm phân tích bài toán | ✅ | `docs/phan-tich-bai-toan.md` (5 vấn đề cốt lõi, stakeholders, 3 máy trạng thái, scope) |
| 6 | Thêm Component/Deployment diagram | ✅ | `docs/diagrams/architecture-diagrams.md` |
| 7 | Sync docs VNPay/Stripe → MoMo | ✅ | SRS, use-cases, sequence, state-machine, BAOCAO, README |
| 8 | Vá class-erd thiếu | ✅ | Thêm entity `RefreshToken` + enum/field `SellerStatus` |

## Verify (mẻ này)
- `dotnet build ECommerce.API`: 4 project, 0 lỗi/cảnh báo
- `npm run build` (client): sạch
- Smoke test API: `pay {momo}` → `requiresRedirect=true` + payUrl `test-payment.momo.vn`; `pay {vnpay}` → 400 (đã gỡ)
- Thanh toán thật MoMo: chọn Ví MoMo → cổng → thẻ ATM test `9704 0000 0000 0018` → order Paid

## Mẻ trước (2026-07-03, ĐÃ COMMIT `caa8f97`)
Seller onboarding (Pending→duyệt), refresh token (15m access + 7d rotate), rate limiting, integration test (SQLite in-mem, 13 test), email mock, upload MIME sniff, trang shop công khai, payment split, Swagger ProducesResponseType. Đặc tả: `docs/tech_specs/2026-07-03-batch-features.md` + `2026-07-03-hardening-batch2.md`.

## Treo (chưa fix)
- MoMo IPN không tới `localhost` — dev chốt đơn qua redirect callback; deploy public/ngrok mới chạy IPN. Chi tiết: `docs/tech_specs/2026-07-05-marketplace-ui-momo-docs.md`.
- Payment split: mới breakdown kế toán, chưa payout thật.
- Flash-sale "giá gốc" là tổng hợp (×1.4) — app chưa có field giá gốc.

## Còn lại
| Task | Trạng thái | Ghi chú |
|------|-----------|---------|
| Commit mẻ 2026-07-05 | ✅ | UI + MoMo + docs reorg |
| Push GitHub + CI thật | ✅ | Agent đã push |
| Dọn `commit.txt` staged | ✅ | Đã xoá file nháp |

## Tài khoản demo (sau seed)
- Admin: `admin@shop.com` / `Admin@123`
- Customer: `user@shop.com` / `User@123`
- Seller (Approved): `seller1@shop.com`, `seller2@shop.com` / `Seller@123`
- Seller mới đăng ký qua UI = Pending, cần Admin duyệt tại `/admin/sellers`
- Thẻ ATM test MoMo (thành công): `9704 0000 0000 0018`, NGUYEN VAN A, 03/07, OTP bất kỳ

---
Xem thêm: `docs/specs/` (đặc tả từng feature), `docs/tech_specs/` (as-built theo mẻ), `docs/DECISIONS.md` (ADR), `docs/GOTCHAS.md` (bẫy đã dính), `docs/phan-tich-bai-toan.md` (phân tích bài toán), `docs/diagrams/` (UML).
