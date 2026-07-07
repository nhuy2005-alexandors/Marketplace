# Auth + RBAC — Spec

> Spec hồi tố từ code hiện có.

## Mục tiêu
Đăng ký/đăng nhập bằng email+password, phát JWT access token + refresh token opaque, cho phép refresh xoay vòng (rotate) và logout thu hồi token, phân quyền theo 3 role (Customer/Admin/Seller) qua `[Authorize(Roles=...)]`, và chặn brute-force bằng rate limiting riêng cho nhóm endpoint auth.

## Yêu cầu
- [x] Đăng ký customer: email chuẩn hoá lowercase, chặn trùng email, hash password bằng BCrypt, role mặc định `Customer` (AuthService.cs:23-39, JwtTokenGenerator.cs:52).
- [x] Đăng ký seller: cùng luồng đăng ký nhưng gán `Role=Seller`, `SellerStatus=Pending` — seller mới chưa bán được ngay, chờ Admin duyệt (AuthService.cs:42-61, User.cs:15-16, SellerAdminService.cs:42-70).
- [x] Login: so email (lowercase) + verify BCrypt hash, sai email/password trả lỗi Unauthorized chung (không lộ email nào sai) (AuthService.cs:63-71).
- [x] JWT access token chứa claims Sub/NameIdentifier/Email/Name/Role, ký HMAC-SHA256, hết hạn theo `Jwt:ExpiryMinutes` (JwtTokenGenerator.cs:17-39, JwtSettings.cs:8, appsettings.json:10 = 15 phút).
- [x] Refresh token: chuỗi random 32 byte base64url (opaque, không phải JWT), hết hạn theo `Jwt:RefreshExpiryDays` (JwtTokenGenerator.cs:41-47, JwtSettings.cs:9, appsettings.json:11 = 7 ngày), lưu DB kèm `UserId`, `ExpiresAt`, `RevokedAt` (RefreshToken.cs:5-15).
- [x] Refresh flow: tìm token theo chuỗi, kiểm `IsActive` (chưa revoke + chưa hết hạn), rotate — revoke token cũ và cấp cặp access+refresh mới (AuthService.cs:81-93, RefreshToken.cs:14).
- [x] Logout: revoke refresh token hiện tại (set `RevokedAt`), idempotent nếu token đã revoke/không tồn tại thì vẫn trả Ok (AuthService.cs:95-104).
- [x] `GET /me` yêu cầu `[Authorize]`, trả `UserDto` từ user id lấy trong JWT claims (AuthController.cs:50-56, AuthService.cs:73-79).
- [x] RBAC bằng role claim trong JWT (`ClaimTypes.Role`) + `[Authorize(Roles="...")]` trên controller, không dùng policy-based authorization — chỉ role check thô (JwtTokenGenerator.cs:25; AdminController.cs:8 role Admin; SellerController.cs:10 role Seller; ProductsController.cs:34,43,53,64 và CategoriesController.cs:19 role Admin,Seller; CouponsController.cs:28,36,46 và OrdersController.cs:74 role Admin).
- [x] Seller được duyệt (`SellerStatus.Approved`) mới được tạo/sửa sản phẩm — check thêm ở service layer ngoài role claim (ProductService.cs:91).
- [x] Rate limiting: toàn API giới hạn 100 req/phút/IP (global fixed window); nhóm `AuthController` (`[EnableRateLimiting("auth")]`) siết chặt hơn 10 req/phút/IP để chống brute-force login/register/refresh (Program.cs:73-86, AuthController.cs:9-10).
- [x] `AddAuthentication` validate đủ Issuer/Audience/Lifetime/SigningKey của JWT (Program.cs:48-61).

## Ràng buộc
- Refresh token là bảng riêng (`RefreshTokens`), không revoke theo user (không có "revoke all sessions"), chỉ theo từng token cụ thể (RefreshToken.cs:5-15, AuthService.cs:95-104).
- Rate limit theo IP (`context.Connection.RemoteIpAddress`), không theo user/email — dùng chung sau proxy/NAT có thể đụng giới hạn sớm (Program.cs:80,84).
- JWT secret nằm trong config (`Jwt:Secret`), không có key rotation hay revoke access token giữa kỳ hạn (access token sống tới khi hết `ExpiryMinutes`, không thể force-logout tức thời) (JwtSettings.cs, JwtTokenGenerator.cs:28-29).
- Login/Register trả message lỗi generic ("Invalid email or password") để không lộ email đã tồn tại khi login sai, nhưng Register lại trả rõ "Email already registered" (Conflict) — không đối xứng (AuthService.cs:27,68).
- Role chỉ có 3 giá trị cố định trong enum, không có custom permission/claim ngoài role (UserRole.cs:3-8).

## Quyết định
- Access token ngắn hạn (15p) + refresh token dài hạn (7 ngày, opaque, lưu DB) — pattern chuẩn để giảm rủi ro lộ access token mà vẫn tiện UX, đổi lại cần 1 round-trip refresh khi access hết hạn.
- Refresh token opaque (random bytes) thay vì JWT thứ hai — đơn giản, so sánh trực tiếp trong DB, không cần verify signature/claims cho refresh.
- Role-based `[Authorize(Roles=...)]` thay vì policy-based — đủ dùng với 3 role cố định, chưa cần độ linh hoạt của policy/claims-based authorization.
- Seller cần trạng thái duyệt riêng (`SellerStatus`) ngoài role — vì "là Seller" (role) và "được phép bán" (approved) là hai điều kiện khác nhau, tách để Admin kiểm soát onboarding.

## Ngoài phạm vi
- Refresh token theo family/device, revoke-all-sessions, hay giới hạn số session đồng thời.
- Email verification, quên/đổi password, 2FA.
- Policy-based/claims-based authorization (permission fine-grained hơn role).
- Access token blacklist/revocation trước khi hết hạn tự nhiên.
