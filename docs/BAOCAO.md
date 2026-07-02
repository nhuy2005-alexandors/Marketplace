# Báo cáo triển khai — MiniShop E-Commerce Platform

**Ngày:** 2026-06-29
**Phạm vi:** Xây dựng nền tảng thương mại điện tử full-stack theo chuẩn thiết kế phần mềm, gồm backend, frontend, tài liệu đặc tả, mở rộng tính năng, và chuyển đổi sang mô hình marketplace nhiều người bán.

---

## 1. Tổng quan

Dự án được thực hiện theo 3 giai đoạn:

**Giai đoạn 1 — Nền tảng cốt lõi:** Xây dựng từ đầu một hệ thống e-commerce tối giản với kiến trúc phân lớp chuẩn (Clean Architecture), đủ 11 chức năng nghiệp vụ (yêu cầu tối thiểu 9, không tính CRUD), kèm bộ tài liệu đặc tả phần mềm (SRS + UML).

**Giai đoạn 2 — Mở rộng:** Bổ sung các hạng mục theo yêu cầu: thanh toán đa cổng thật (VNPay + Stripe), hệ thống mã giảm giá, upload ảnh sản phẩm, phân trang đơn hàng, giao diện quản trị CRUD, và pipeline CI/CD.

**Giai đoạn 3 — Marketplace nhiều người bán:** Chuyển từ mô hình single-store (1 admin quản mọi thứ) sang marketplace: thêm vai trò **Seller**, mỗi seller tự đăng ký, quản sản phẩm/đơn/doanh thu riêng của cửa hàng mình; một đơn hàng có thể trộn sản phẩm từ nhiều seller (mỗi `OrderItem` gắn `SellerId`), seller chỉ thấy phần của mình. Admin vẫn quản toàn sàn.

> **Ghi chú phạm vi:** Ứng dụng chạy trực tiếp trên máy (local) — backend qua `dotnet run` + SQL Server LocalDB, frontend qua `npm run dev`. Không dùng Docker.

---

## 2. Kiến trúc hệ thống

### 2.1 Backend — ASP.NET Core 9 + EF Core, kiến trúc 4 lớp

```
ECommerce.sln
├── ECommerce.Domain          Entities thuần, enum, business rules (không phụ thuộc framework)
├── ECommerce.Application     DTOs, service interfaces + implementations, validators, Result pattern
├── ECommerce.Infrastructure  EF Core DbContext, JWT, payment providers, file storage, seed data
└── ECommerce.API             Controllers, middleware, DI wiring, Swagger, JWT auth
```

Nguyên tắc phụ thuộc: `API → Application → Domain`, `Infrastructure → Application interfaces`. Domain không biết gì về EF Core hay HTTP — toàn bộ business logic (ví dụ máy trạng thái đơn hàng) là code thuần C#, test được độc lập không cần database thật.

### 2.2 Frontend — React 18 + Vite + TypeScript + TailwindCSS

SPA với React Router (protected routes theo role), TanStack Query (cache + invalidation dữ liệu server), Zustand (auth state có persist localStorage), Axios (interceptor tự đính JWT + xử lý 401).

### 2.3 Cơ sở dữ liệu — SQL Server qua EF Core Migrations

Toàn bộ schema được quản lý qua 4 migration (`InitialCreate`, `AddCouponsAndDiscounts`, `AddSellerMarketplace`, `AddOrderItemFulfillment`), có thể chạy lại từ đầu trên máy sạch bằng `dotnet ef database update`.

---

## 3. Chức năng nghiệp vụ đã triển khai

| # | Chức năng | Mô tả kỹ thuật |
|---|-----------|-----------------|
| 1 | Đăng ký / Đăng nhập | JWT Bearer, mật khẩu hash BCrypt, claim chứa role |
| 2 | Phân quyền RBAC | `[Authorize(Roles="Admin")]` theo policy, tách rõ Customer/Admin |
| 3 | Tìm kiếm/lọc/phân trang sản phẩm | Query theo tên, danh mục, khoảng giá; sort; `PagedResult<T>` |
| 4 | Giỏ hàng | Thêm/sửa/xóa item, validate tồn kho real-time |
| 5 | Đặt hàng (Checkout) | Chuyển giỏ → đơn, trừ tồn kho nguyên tử, snapshot giá/tên sản phẩm vào OrderItem |
| 6 | **Thanh toán đa cổng** | Abstraction `IPaymentProvider`: Mock, COD, **VNPay** (HMAC-SHA512, redirect + verify), **Stripe** (Checkout Session, verify qua session_id) |
| 7 | Vòng đời đơn hàng | State machine trong Domain (`Order.ChangeStatus`), chặn transition sai bằng exception → HTTP 409 |
| 8 | Đánh giá sản phẩm | Rating 1–5 + comment, ràng buộc "verified purchase" (phải có đơn chứa SP, khác Cancelled), chống trùng |
| 9 | Wishlist | Thêm/xóa sản phẩm yêu thích |
| 10 | Dashboard quản trị | Doanh thu (từ Payment Completed), đơn theo trạng thái, top 5 SP bán chạy |
| 11 | CRUD sản phẩm/danh mục | Đầy đủ Create/Update/Delete, ràng buộc không xóa Category còn Product |
| 12 | **Mã giảm giá (Coupon)** | Percentage/FixedAmount, đơn tối thiểu, hạn dùng, giới hạn số lượt, validate trước khi checkout |
| 13 | **Upload ảnh sản phẩm** | `IFormFile` → lưu local disk, trả URL, giới hạn định dạng + kích thước 5MB |
| 14 | **Phân trang đơn hàng** | `GET /api/orders?page=&pageSize=` cho cả khách và admin |

---

## 4. Chi tiết kỹ thuật các hạng mục mở rộng

### 4.1 Thanh toán đa cổng (VNPay + Stripe)

Thiết kế theo pattern **Strategy + Factory**:

- `IPaymentProvider` — interface chung: `CreatePaymentAsync` (khởi tạo, trả redirect URL hoặc hoàn tất ngay) và `VerifyAsync` (xác minh callback).
- `PaymentProviderFactory` — resolve provider theo key (`mock`/`cod`/`vnpay`/`stripe`) từ danh sách đăng ký DI.
- `VnPayProvider` — build URL redirect ký HMAC-SHA512 đúng chuẩn VNPay sandbox; verify chữ ký khi khách quay lại qua `GET /api/payments/vnpay/callback`.
- `StripeProvider` — dùng SDK `Stripe.net`, tạo Checkout Session, verify bằng `session_id` qua `GET /api/payments/stripe/callback`.
- **Fallback demo:** Khi chưa cấu hình `TmnCode`/`SecretKey`, cả 2 provider tự chuyển sang chế độ hoàn tất ngay (không lỗi, không cần key) — cho phép demo toàn bộ luồng ngay lập tức, và chỉ cần điền config để chuyển sang sandbox/thật.
- `PaymentService` được viết lại theo 2 method: `InitiateAsync` (khởi tạo, có thể trả redirect) và `ConfirmAsync` (xử lý callback, chốt `Order → Paid`).

Đã verify thật: pay qua `mock` hoàn tất ngay (status → Paid), pay qua `vnpay` khi chưa cấu hình key cũng tự hoàn tất demo đúng như thiết kế.

### 4.2 Mã giảm giá (Coupon)

- Entity `Coupon` trong Domain với logic tự chứa: `IsValidFor()` (check active, hết hạn, đơn tối thiểu, còn lượt) và `CalculateDiscount()`.
- `Order` được bổ sung `CouponCode`, `DiscountAmount`; `Subtotal` (tổng trước giảm) và `Total` (sau giảm) tách riêng.
- Flow: Customer nhập mã ở trang Checkout → gọi `POST /api/coupons/validate` để preview số tiền giảm → khi đặt hàng, `CheckoutRequest` mang theo `CouponCode`, `OrderService` validate lại lần 2 (chống race condition/hết lượt giữa lúc preview và khi đặt) rồi mới trừ giảm giá và tăng `TimesUsed`.
- Admin có trang tạo/xóa coupon riêng.
- Verify thật: coupon `WELCOME10` (giảm 10%) áp dụng đúng — đơn subtotal $199 → discount $19.9 → total $179.1.

### 4.3 Upload ảnh sản phẩm

- `IFileStorage.SaveImageAsync` (interface trong Application) — implementation `LocalFileStorage` lưu vào `wwwroot/uploads`, giới hạn định dạng ảnh hợp lệ, sinh tên file GUID tránh trùng/đè.
- Endpoint `POST /api/products/upload-image` (Admin only, giới hạn 5MB) trả về URL tương đối, gán vào field `ImageUrl` khi tạo/sửa sản phẩm.
- Frontend: input file trong trang Admin Products, preview ảnh ngay sau khi upload thành công.
- `app.UseStaticFiles()` được thêm vào pipeline để serve file tĩnh qua URL `/uploads/...`.

### 4.4 Phân trang đơn hàng

- `IOrderService.GetForUserAsync`/`GetAllAsync` đổi từ trả `List` sang trả `PagedResult<OrderDto>` (tái dùng pattern `PagedResult` đã có từ sản phẩm).
- Controller nhận `page`, `pageSize` qua query string.
- Frontend `OrdersPage` có control chuyển trang, giữ nguyên UI hiển thị breakdown subtotal/discount/total mới.

### 4.5 Giao diện quản trị CRUD

- Trang mới `AdminProductsPage`: form thêm/sửa sản phẩm (kèm chọn danh mục, upload ảnh preview), danh sách sản phẩm với nút Sửa/Xóa, panel quản lý danh mục (thêm/xóa) cạnh bên.
- Trang mới `AdminCouponsPage`: form tạo coupon (loại giảm giá, giá trị, đơn tối thiểu, số lượt, hạn dùng), danh sách coupon hiện có kèm nút xóa.
- Navbar bổ sung 2 link điều hướng cho Admin.

### 4.6 Auto-migrate khi khởi động

`Program.cs` chạy migrate + seed database tự động mỗi lần khởi động (idempotent — seed chỉ chèn khi bảng rỗng). Nhờ vậy chỉ cần `dotnet run` là DB tự tạo schema + dữ liệu mẫu, không phải chạy `dotnet ef database update` thủ công.

### 4.7 CI/CD — GitHub Actions

`.github/workflows/ci.yml` — 2 job chạy khi push/PR vào `main`/`master`:
1. **backend** — `dotnet restore/build/test`, xuất kết quả test dạng `.trx` làm artifact.
2. **frontend** — `npm ci`, `npm run build` (bao gồm type-check `tsc -b`).

### 4.8 Marketplace nhiều người bán

- **Vai trò Seller** thêm vào `UserRole` enum; `User` có `ShopName`. Tự đăng ký qua `POST /api/auth/register-seller` (không cần Admin duyệt).
- **Sở hữu sản phẩm:** `Product.SellerId` (FK → User). Product CRUD mở cho `Admin,Seller`; seller chỉ sửa/xóa sản phẩm của mình (`ProductService` kiểm tra `actorId == SellerId`, sai → 403). Admin sửa được mọi sản phẩm. Search hỗ trợ lọc `sellerId`.
- **Đơn trộn nhiều seller:** mỗi `OrderItem` snapshot `SellerId` lúc checkout. Một đơn có thể chứa item của nhiều shop. `SellerOrderService.GetForSellerAsync` trả các đơn chứa item của seller, nhưng chỉ hiển thị item + doanh thu phần của seller đó.
- **Dashboard scope:** `DashboardService.GetAsync(int? sellerId)` — `null` = toàn hệ thống (Admin), có giá trị = chỉ dữ liệu seller. `SellerController` (`/api/seller/dashboard`, `/api/seller/orders`) chỉ cho role Seller.
- **Frontend:** đăng ký seller (thêm mode trong LoginPage), 3 trang seller (dashboard, quản lý SP của tôi, đơn hàng), điều hướng + protected route theo role. `ProductCard`/cart chỉ hiện cho Customer.
- **Kiểm chứng thật (qua HTTP):** seller2 xóa SP của seller1 → 403; admin xóa → 200; đơn trộn TechZone + BookHaven → seller1 chỉ thấy "Wireless Headphones", seller2 chỉ thấy "Clean Code".

YAML đã được validate cú pháp; các lệnh bên trong (`dotnet build/test`, `npm run build`) đều đã chạy thành công thật trong quá trình phát triển.

---

### 4.9 Giao hàng theo item + Seller tạo danh mục + fix pro-rate coupon (giai đoạn 4)

- **Per-item fulfillment:** Thêm enum `FulfillmentStatus` (Pending/Shipped/Delivered/Cancelled) và `OrderItem.ChangeStatus()` — máy trạng thái thứ ba của hệ thống, cùng pattern với `Order.ChangeStatus()` nhưng **độc lập hoàn toàn**. `Order.Status` vẫn chỉ Admin đổi (vòng đời thanh toán toàn đơn); `OrderItem.Status` do **Seller sở hữu item đó** tự cập nhật qua `PUT /api/seller/orders/items/{itemId}/status` — mỗi seller ship phần của mình trong một đơn trộn nhiều seller mà không cần chờ hay phụ thuộc seller khác. Item không thuộc seller gọi request → 403; chuyển sai cạnh (ví dụ Pending→Delivered) → 409. Khi Customer hủy đơn, mọi item còn Pending tự chuyển sang Cancelled.
- **Seller tạo danh mục:** `POST /api/categories` mở từ `Admin` sang `Admin,Seller` — Seller tạo danh mục mới ngay lúc thêm sản phẩm, không phải chờ Admin duyệt trước. `DELETE /api/categories/{id}` vẫn giữ nguyên Admin-only.
- **Fix bug pro-rate giảm giá coupon cho seller:** Trước đây `SellerOrderService.GetForSellerAsync` và `DashboardService.GetAsync(sellerId)` bỏ qua `Order.DiscountAmount` khi tính doanh thu/subtotal theo seller — seller thấy doanh thu cao hơn thực nhận vì không trừ phần giảm giá coupon. Sửa: chia tỉ lệ giảm giá theo tỉ trọng subtotal của seller trong đơn (`sellerDiscount = DiscountAmount × sellerSubtotal / orderSubtotal`), rồi `sellerTotal = sellerSubtotal − sellerDiscount`. Dashboard hệ thống (Admin) cũng đổi sang tính doanh thu **sau** giảm giá (`Σ Order.Total` của đơn đã thanh toán) thay vì tổng subtotal thô.
  - **Đã verify bằng test (`FulfillmentTests.CouponDiscount_IsProRatedPerSeller`, `Dashboard_Revenue_IsAfterCouponDiscount`):** đơn $100 gồm seller A (sản phẩm $60) + seller B (sản phẩm $40), coupon giảm cố định $20 → seller A thấy discount $12, total $48; seller B thấy discount $8, total $32; tổng $48 + $32 = $80, khớp đúng `Order.Total` ($100 − $20) mà hệ thống thực nhận. Dashboard Admin (`sellerId = null`) báo doanh thu $80; dashboard seller A báo $48.
- `OrderItemDto` bổ sung `Id` và `Status` để frontend hiển thị và gọi API đổi trạng thái theo từng item.

---

## 5. Kiểm thử

- **Unit test (49 test, 100% pass):** state machine đơn hàng (mọi transition hợp lệ/không hợp lệ), giảm tồn kho, checkout (giỏ rỗng, vượt tồn kho, áp coupon hợp lệ/không hợp lệ), thanh toán (hoàn tất, chuyển trạng thái), hủy đơn (hoàn tồn kho), review (chặn chưa mua, chặn trùng), auth (đăng ký trùng email, sai mật khẩu), seller scope (gán SellerId, chặn sửa/xóa SP của seller khác → 403, admin toàn quyền, lọc theo seller, dashboard scope), **`FulfillmentTests` mới (10 test): máy trạng thái `OrderItem` (mọi transition hợp lệ/không hợp lệ), seller chỉ fulfill item của mình (403 nếu không), coupon pro-rate theo seller ($60/$40 split, coupon $20 → $48/$32), dashboard doanh thu sau giảm giá (system $80, seller A $48).**
- **Smoke test thủ công qua HTTP thật** (không phải giả định): toàn bộ golden path đăng nhập → cart → checkout với coupon → pay đa phương thức → admin đổi trạng thái → dashboard, chạy trên môi trường local (`dotnet run` + `npm run dev`).
- **Frontend:** `tsc -b && vite build` pass, không lỗi type.

---

## 6. Cấu trúc file thay đổi/thêm mới (giai đoạn mở rộng)

**Backend:**
`Domain/Entities/Coupon.cs`, `Application/DTOs/Coupons/*`, `Application/Services/CouponService.cs`, `Application/Services/PaymentService.cs` (viết lại), `Application/Interfaces/IServices.cs` + `IBusinessServices.cs` (mở rộng), `Infrastructure/Payments/{VnPayProvider,StripeProvider,MockPaymentProvider,PaymentProviderFactory,PaymentOptions}.cs`, `Infrastructure/Storage/LocalFileStorage.cs`, `API/Controllers/{PaymentsController,CouponsController}.cs`, migration `AddCouponsAndDiscounts`.

**Frontend:**
`pages/{AdminProductsPage,AdminCouponsPage}.tsx` (mới), `pages/{CheckoutPage,OrdersPage}.tsx` (viết lại), `api/hooks.ts` (mở rộng ~15 hook mới), `types.ts` (mở rộng).

**Marketplace (giai đoạn 3):**
Backend — `Domain/Entities/{User,Product,OrderItem}.cs` + `Enums/UserRole.cs` (thêm SellerId/ShopName/role Seller), `Application/Services/{SellerOrderService,DashboardService,ProductService,AuthService}.cs`, `API/Controllers/SellerController.cs`, migration `AddSellerMarketplace`, `tests/.../SellerScopeTests.cs` (6 test).
Frontend — `pages/{SellerDashboardPage,SellerProductsPage,SellerOrdersPage}.tsx` (mới), `LoginPage.tsx` (thêm mode đăng ký seller), `ProtectedRoute.tsx` (roles[]).

**Per-item fulfillment + Seller category + coupon pro-rate fix (giai đoạn 4):**
Backend — `Domain/Enums/FulfillmentStatus.cs` (mới), `Domain/Entities/OrderItem.cs` (thêm `Status` + `ChangeStatus()`), `Application/Services/{SellerOrderService,DashboardService,OrderService}.cs` (pro-rate coupon, hủy đơn set Cancelled cho item Pending), `Application/DTOs/Orders/OrderDtos.cs` (`OrderItemDto` thêm `Id`, `Status`), `API/Controllers/{SellerController,CategoriesController}.cs` (thêm `PUT orders/items/{itemId}/status`; mở `POST /api/categories` cho Seller), `tests/.../FulfillmentTests.cs` (mới, 10 test).

**Hạ tầng:**
`.github/workflows/ci.yml`.

---

## 7. Giới hạn & khuyến nghị tiếp theo

- VNPay/Stripe hiện chạy chế độ demo khi chưa có key thật — cần merchant account thật để test end-to-end với cổng thật (sandbox VNPay hoặc Stripe test mode).
- File upload lưu local disk (`wwwroot/uploads`) — khi cần scale nhiều instance nên chuyển sang object storage (S3/Azure Blob).
- CI/CD chỉ build + test, chưa có bước deploy — có thể bổ sung khi có môi trường staging/production.
- Ứng dụng chạy local; nếu cần triển khai lên server có thể đóng gói lại (Docker hoặc publish trực tiếp) về sau.
