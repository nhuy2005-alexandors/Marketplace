# Task Checkpoint — MiniShop

Cập nhật: 2026-07-03

## Đã xong mẻ này (chưa commit)

| # | Task | Trạng thái | Ghi chú |
|---|------|-----------|---------|
| 1 | Upload validate MIME/content | ✅ | Magic-byte sniff, suy đuôi từ nội dung, 5MB, chặn file giả |
| 2 | Trang shop công khai `/shop/:sellerId` | ✅ | `GET /api/sellers/{id}/shop` + trang FE, tái dùng product search |
| 3 | Swagger `ProducesResponseType` | ✅ | ~40 action / 10 controller |
| 4 | Chia tiền theo seller | ✅ | `GET /api/orders/{id}/split`, pro-rate + bù dư làm tròn, 5 test |
| 5 | Seller onboarding (Pending→Admin duyệt) | ✅ | Flow [1]. Enum SellerStatus, gate product-create, ẩn shop, trang Admin duyệt, banner seller, 8 test, migration |

## Mẻ hardening #2 (chưa commit) — `docs/tech_specs/2026-07-03-hardening-batch2.md`

| # | Task | Trạng thái | Ghi chú |
|---|------|-----------|---------|
| R | Review toàn dự án + fix | ✅ | 2 audit agent. 5 fix: gate null-actor, cancel concurrency 409, split discount clamp, round discount tại nguồn, FE stale sellerStatus (useMe sync). |
| 6 | Refresh token + logout/revoke | ✅ | Access 15m + refresh 7d rotate, revoke on logout, entity+migration, axios auto-refresh interceptor, 3 test |
| 7 | Rate limiting | ✅ | Global 100/min/IP + policy "auth" 10/min trên AuthController, 429 |
| 8 | Integration test | ✅ | WebApplicationFactory + SQLite in-mem, 13 test (auth/refresh/catalog/authz) |
| 9 | Email/notification (mock) | ✅ | IEmailSender + LoggingEmailSender, wire checkout + approve-seller, best-effort |

Đặc tả: `docs/tech_specs/2026-07-03-batch-features.md` (mẻ 1), `docs/tech_specs/2026-07-03-hardening-batch2.md` (mẻ 2).

## Verify (mới nhất)
- `dotnet build`: 6 project, 0 lỗi/cảnh báo
- `dotnet test`: **78/78 pass** (65 unit + 13 integration)
- `npm run build`: sạch
- Migration: `AddSellerApprovalStatus`, `AddRefreshTokens` (an toàn dữ liệu cũ)

## Treo (chưa fix)
- Demo payment callback `[AllowAnonymous]` chỉ gate `IsDevelopment()` — cần signed nonce per-order để hardening thật (MED, dev-only nên an toàn hiện tại).

## Còn lại

| Task | Trạng thái | Ghi chú |
|------|-----------|---------|
| Push GitHub + CI thật | ⏳ | **User tự push** (đã chốt) |

## Tài khoản demo (sau seed)
- Admin: `admin@shop.com` / `Admin@123`
- Customer: `user@shop.com` / `User@123`
- Seller (Approved): `seller1@shop.com`, `seller2@shop.com` / `Seller@123`
- Seller mới đăng ký qua UI = Pending, cần Admin duyệt tại `/admin/sellers`

## Việc tồn kho (từ checkpoint trước)
- Xem `memory/ecommerce-minishop-checkpoint.md`
