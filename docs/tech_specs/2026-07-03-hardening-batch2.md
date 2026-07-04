# Đặc tả kỹ thuật — Mẻ hardening #2 (2026-07-03)

**Phiên bản:** 1.0
**Ngày:** 2026-07-03
**Phạm vi:** Review toàn dự án + 4 tính năng: refresh token, rate limiting, integration test, email/notification.

Bắt đầu bằng audit toàn dự án (2 agent read-only: backend logic/security + frontend contract/drift), sửa các lỗi xác nhận, rồi làm 4 feature.

---

## 0. Review & fix (audit-driven)

2 audit agent quét song song. Không có lỗi CRITICAL/HIGH backend. Các fix đã áp:

| Fix | File | Vấn đề → sửa |
|-----|------|--------------|
| Gate null actor | `ProductService.CreateAsync` | Pattern `actor is { Role: Seller, ... }` false khi actor null → create lọt. Thêm null-check → `NotFound`. |
| Cancel concurrency | `OrderService.CancelAsync` | Không catch `DbUpdateConcurrencyException` như Checkout → 2 cancel song song crash 500. Bọc try/catch → `Conflict`. |
| Split discount âm | `OrderService.GetSplitAsync` | Phần dư làm tròn của seller cuối có thể âm với 3+ seller. `Math.Clamp(.., 0, sellerSubtotal)`. |
| Round discount tại nguồn | `Coupon.CalculateDiscount` | Trả >2 chữ số thập phân → lan xuống split. `Math.Round(.., 2, AwayFromZero)`. |
| Stale sellerStatus (FE HIGH) | `store/auth.ts`, `hooks.ts useMe`, seller pages | Sau khi Admin duyệt, seller phải logout/login mới hết banner. Thêm `setUser`, `useMe` tự sync store, seller pages gọi `useMe` để refetch `/auth/me`. |

**Còn treo (MED, dev-only):** demo payment callback `[AllowAnonymous]` chỉ gate bằng `IsDevelopment()`, không có nonce/secret per-order. An toàn hiện tại (chỉ Development) nhưng nếu env cấu hình sai là lỗ. Fix thật cần signed one-time token — ngoài scope mẻ này.

---

## 1. Refresh token + logout/revoke

### Vấn đề
JWT trước là access-token đơn, `ExpiryMinutes=1440` (1 ngày). Hết hạn phải login lại; không revoke được.

### Giải pháp
- Access token rút xuống **15 phút**; thêm **refresh token 7 ngày** (opaque random 32 byte base64url, lưu DB).
- Entity `RefreshToken` (UserId, Token unique, ExpiresAt, RevokedAt; `IsActive` computed). Migration `AddRefreshTokens`.
- `IJwtTokenGenerator.GenerateRefreshToken()` sinh token + expiry (RNG crypto).
- `AuthService.BuildAsync` cấp cả access + refresh, lưu refresh vào DB. Login/register/register-seller đều trả cặp.
- **Rotate:** `RefreshAsync` — refresh hợp lệ → thu hồi token cũ (`RevokedAt`), cấp cặp mới. Token cũ dùng lại → `Unauthorized`.
- `LogoutAsync` — thu hồi refresh token.
- Endpoint: `POST /api/auth/refresh`, `POST /api/auth/logout`.
- **Frontend:** auth store lưu `refreshToken` + `setToken`. Axios interceptor: gặp 401 → gọi `/auth/refresh` (1 lần, dedupe qua promise dùng chung), set token mới, retry request gốc; fail → logout. Navbar logout gọi `/auth/logout` revoke server-side.

### File
- `Domain/Entities/RefreshToken.cs`, `Persistence/AppDbContext.cs`, `IAppDbContext.cs`, `Configurations/CatalogConfigurations.cs`, migration `AddRefreshTokens`
- `Interfaces/IServices.cs` (IJwtTokenGenerator), `Auth/JwtTokenGenerator.cs`, `Auth/JwtSettings.cs`, `AuthService.cs`, `DTOs/Auth/AuthDtos.cs`, `Interfaces/IBusinessServices.cs`, `AuthController.cs`, `appsettings.json`
- Client: `store/auth.ts`, `api/client.ts`, `api/hooks.ts`, `components/Navbar.tsx`, `types.ts`
- Test: 3 test mới trong `AuthServiceTests.cs` (rotate, reuse-rejected, logout-revoke)

### Bất biến test khóa
- Login trả refreshToken. Refresh rotate (token mới ≠ cũ); dùng lại cũ → Unauthorized. Logout → refresh sau đó → Unauthorized.

---

## 2. Rate limiting

### Vấn đề
Không giới hạn tần suất → brute-force login, spam API.

### Giải pháp
`AddRateLimiter` (built-in .NET) trong Program.cs:
- **Global:** fixed-window per-IP, 100 req/phút.
- **Policy "auth":** 10 req/phút/IP, gắn `[EnableRateLimiting("auth")]` lên `AuthController` (login/register/refresh) chống brute-force.
- Vượt → `429 Too Many Requests`. `app.UseRateLimiter()` sau CORS, trước Authentication.

### File
- `Program.cs`, `AuthController.cs`

---

## 3. Integration test (WebApplicationFactory)

### Vấn đề
Chỉ có unit test service; chưa test end-to-end qua HTTP thật (middleware, auth, serialization, DI).

### Giải pháp
- `CustomWebApplicationFactory : WebApplicationFactory<Program>` (dùng `public partial class Program` có sẵn).
- **DB test:** SQLite in-memory (1 connection giữ mở suốt vòng đời factory) + `EnsureCreated()` sinh schema theo model, rồi stamp `__EFMigrationsHistory` để `MigrateAsync()` của app thành no-op an toàn — **không sửa code production**. (InMemory provider không dùng được vì `MigrateAsync` ném; migration SqlServer-only SQL nên SQLite replay verbatim cũng lỗi.)
- 13 test: auth flow (register/login/sai mật khẩu/me), refresh flow (rotate + reuse-rejected + logout), catalog (list/by-id/404), authorization (admin dashboard 401/403/200).

### File
- `tests/ECommerce.Tests/Integration/` (CustomWebApplicationFactory, AuthFlowTests, RefreshFlowTests, CatalogTests, AuthorizationTests)
- `ECommerce.Tests.csproj` (+ `Microsoft.EntityFrameworkCore.Sqlite`, `Microsoft.AspNetCore.Mvc.Testing`)

---

## 4. Email/notification (mock)

### Vấn đề
Không thông báo cho khách/seller khi có sự kiện.

### Giải pháp
- Interface `IEmailSender.SendAsync(to, subject, body, ct)` (Application layer).
- Mock `LoggingEmailSender` (Infrastructure) — log ra `ILogger` mức Information. Dễ swap SMTP thật sau.
- DI: `AddScoped<IEmailSender, LoggingEmailSender>()`.
- **Wire 2 sự kiện** (best-effort, try/catch nuốt lỗi — không làm fail nghiệp vụ):
  1. `OrderService.CheckoutAsync` — sau khi lưu đơn, gửi "Xác nhận đơn hàng #{id}" tới email khách.
  2. `SellerAdminService.ApproveAsync` — sau khi duyệt, gửi "Cửa hàng của bạn đã được duyệt" tới seller.
- **Tương thích test:** ctor thêm `IEmailSender? email = null` + private `NullEmailSender` no-op fallback → `new OrderService(db)` cũ vẫn compile, DI truyền LoggingEmailSender thật ở production.

### File
- `Interfaces/IServices.cs` (IEmailSender), `Infrastructure/Notifications/LoggingEmailSender.cs`, `Infrastructure/DependencyInjection.cs`, `OrderService.cs`, `SellerAdminService.cs`

---

## Kiểm chứng mẻ này
- `dotnet build`: 6 project, 0 lỗi, 0 cảnh báo.
- `dotnet test`: **78/78 pass** (65 unit + 13 integration).
- `npm run build` (client): sạch.
- Migration mới: `AddRefreshTokens` (bảng RefreshTokens).
