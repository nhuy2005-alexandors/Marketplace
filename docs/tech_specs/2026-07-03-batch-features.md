# Đặc tả kỹ thuật — Mẻ tính năng 2026-07-03

**Phiên bản:** 1.0
**Ngày:** 2026-07-03
**Phạm vi:** 4 tính năng: (1) Trang shop công khai, (2) Kiểm tra nội dung ảnh upload, (3) Chú thích Swagger, (4) Chia tiền theo seller.

Tài liệu này giải thích *lý do* và *cách hoạt động* của từng thay đổi cho người mới đọc code. Xem `SRS.md` cho bối cảnh nghiệp vụ tổng thể.

---

## 1. Trang shop công khai `/shop/:sellerId`

### Vấn đề
Khách xem sản phẩm nhưng không có cách xem toàn bộ hàng của một cửa hàng. `Product` đã có `SellerId`, `ProductQuery` đã lọc theo `SellerId`, nhưng không có endpoint trả thông tin public của seller (tên shop) và không có trang hiển thị.

### Giải pháp
- **Backend:** endpoint public (không auth) `GET /api/sellers/{id}/shop` trả `SellerShopDto(SellerId, ShopName)`.
  - Service `IProductService.GetSellerShopAsync` truy vấn `Users` với `Role == Seller`, `AsNoTracking`. Không phải seller → `NotFound`.
  - `ShopName` fallback về `FullName` khi seller chưa đặt tên shop.
  - Danh sách sản phẩm **tái dùng** `GET /api/products?sellerId=` — không tạo endpoint mới cho product.
- **Frontend:** trang `SellerShopPage` (route `/shop/:sellerId`) gọi `useSellerShop` + `useProducts({ sellerId })`, mirror layout `ProductListPage` (grid + phân trang). Tên shop trên `ProductCard` giờ link tới `/shop/{sellerId}`.

### File
- `src/ECommerce.Application/DTOs/Catalog/CatalogDtos.cs` — `SellerShopDto`
- `src/ECommerce.Application/Services/ProductService.cs` — `GetSellerShopAsync`
- `src/ECommerce.API/Controllers/SellersController.cs` — controller mới
- `client/src/pages/SellerShopPage.tsx`, `client/src/api/hooks.ts` (`useSellerShop`), `client/src/App.tsx` (route), `client/src/components/ProductCard.tsx`

### Lưu ý bảo mật
Endpoint public không lộ email/thông tin nhạy cảm — chỉ trả `SellerId` + `ShopName`.

---

## 2. Kiểm tra nội dung ảnh upload (magic-byte)

### Vấn đề
`LocalFileStorage.SaveImageAsync` chỉ kiểm phần mở rộng tên file. Kẻ tấn công đổi tên `evil.html`/`shell.svg` thành `.jpg` sẽ qua được — phần mở rộng có thể giả mạo (spoofable). File độc lưu vào `wwwroot/uploads` (phục vụ tĩnh) là rủi ro XSS/thực thi.

### Giải pháp
Đọc file vào `MemoryStream` (stream tới có thể forward-only), rồi **sniff magic bytes** để xác định loại thật:

| Loại | Chữ ký (hex) |
|------|--------------|
| JPEG | `FF D8 FF` |
| PNG  | `89 50 4E 47 0D 0A 1A 0A` |
| GIF  | `47 49 46 38 37/39 61` (GIF87a/GIF89a) |
| WEBP | `52 49 46 46` (RIFF) tại 0-3 **và** `57 45 42 50` (WEBP) tại 8-11 |

- Extension lưu được **suy ra từ magic bytes** (không tin tên file gốc) → file gắn sai đuôi bị đổi về đuôi đúng, file không phải ảnh bị từ chối `InvalidOperationException("Unsupported or invalid image content.")`.
- Giới hạn 5 MB kiểm ở tầng storage (defense-in-depth; controller đã có `[RequestSizeLimit(5_000_000)]`).
- File rỗng → từ chối.

### File
- `src/ECommerce.Infrastructure/Storage/LocalFileStorage.cs` — viết lại `SaveImageAsync`, thêm helper `DetectImageExtension`.

---

## 3. Chú thích Swagger `[ProducesResponseType]`

### Vấn đề
Controller không khai báo mã trạng thái/kiểu response → Swagger UI chỉ hiện 200 mặc định, người tích hợp API không biết các mã lỗi (400/401/403/404/409) hay hình dạng dữ liệu.

### Giải pháp
Chú thích mọi action theo quy tắc bám sát `ApiControllerBase.MapError` (map `ErrorType` → mã HTTP):
- Success: `[ProducesResponseType(typeof(TDto), 200)]` (hoặc không kèm type nếu trả `Ok()`).
- Có `[Authorize]` → thêm 401; `[Authorize(Roles=...)]` → thêm 403.
- Action tra cứu theo id / có thể `NotFound` → 404.
- Action nhận body/validate → 400; chỉ thêm 409 nơi service thật sự trả `ErrorType.Conflict` (xóa product dính FK, checkout hết hàng, hủy đơn sai trạng thái, redeem coupon...).

Không đổi logic/chữ ký — chỉ thêm attribute.

### File
~40 action trên 10 controller trong `src/ECommerce.API/Controllers/`.

---

## 4. Chia tiền theo seller (payment split)

### Vấn đề
Một `Order` chứa item của **nhiều seller**. Cần breakdown số tiền mỗi seller nhận (subtotal, phần giảm giá được chia, net) cho mục đích đối soát/hiển thị. Chưa có payout gateway thật → đây là **breakdown kế toán**, không chuyển tiền.

### Giải pháp
- Endpoint `GET /api/orders/{id}/split` (auth) trả `OrderSplitDto`. Access: **admin** hoặc **chủ đơn** (giống `GetByIdAsync`).
- `IOrderService.GetSplitAsync` gom `OrderItem` theo `SellerId`, mỗi nhóm tính:
  - `Subtotal` = tổng item của seller.
  - `DiscountShare` = **pro-rate** giảm giá toàn đơn theo tỉ trọng subtotal của seller.
  - `NetTotal` = `max(0, Subtotal - DiscountShare)`.
- **Bù dư làm tròn (rounding remainder):** seller cuối cùng nhận `DiscountAmount - đã_phân_bổ` để tổng các phần chia **khớp chính xác** giảm giá toàn đơn (tránh lệch xu do `Math.Round` khi chia không hết, vd 10 chia cho 100/33.33).
- Tên shop tra cứu theo batch từ `Users`, fallback `FullName` rồi `Seller #{id}`.

### Bất biến (invariant) được test khóa
- `Σ DiscountShare == Order.DiscountAmount` (chính xác).
- `Σ NetTotal == Order.Total`.
- `NetTotal == Subtotal - DiscountShare` cho mọi seller.
- Người khác chủ đơn (không admin) → `Forbidden`. Đơn không tồn tại → `NotFound`.

### File
- `src/ECommerce.Application/DTOs/Orders/OrderDtos.cs` — `SellerSplitDto`, `OrderSplitDto`
- `src/ECommerce.Application/Services/OrderService.cs` — `GetSplitAsync`
- `src/ECommerce.API/Controllers/OrdersController.cs` — action `Split`
- `client/src/components/OrderSplitPanel.tsx` (lazy-load khi mở), `OrdersPage.tsx`, `hooks.ts` (`useOrderSplit`), `types.ts`
- Test: `tests/ECommerce.Tests/Unit/PaymentSplitTests.cs` (5 test)

---

---

## 5. Seller onboarding — duyệt bởi Admin

### Vấn đề
Trước đây `register-seller` tạo seller **active ngay**, ai cũng bán được không kiểm soát. Cần cổng duyệt: seller mới ở trạng thái chờ, Admin duyệt mới bán.

### Giải pháp (flow: Pending → Admin duyệt)
- **Enum mới** `SellerStatus { Pending, Approved }`. Trường `User.SellerStatus` **nullable**: `null` = không phải seller; seller mới = `Pending`; sau duyệt = `Approved`. Migration `AddSellerApprovalStatus` thêm cột `int?` (nullable → không phá dữ liệu cũ).
- **Đăng ký:** `AuthService.RegisterSellerAsync` gán `SellerStatus = Pending`. `UserDto` thêm field `SellerStatus` (client biết trạng thái).
- **Gate đăng sản phẩm:** `ProductService.CreateAsync` chặn seller chưa `Approved` → `Forbidden` ("Tài khoản seller chưa được duyệt."). **Admin bỏ qua** (Admin có `SellerStatus == null`, không phải role Seller).
- **Ẩn shop chưa duyệt:** `GetSellerShopAsync` chỉ trả seller `Approved` → shop pending trả `NotFound` (trang `/shop/:id` hiện "không tìm thấy").
- **Admin duyệt:** service mới `ISellerAdminService`:
  - `GetSellersAsync(status?)` — list seller, lọc theo trạng thái.
  - `ApproveAsync(id)` — Pending → Approved; đã Approved → `Conflict`; không phải seller → `NotFound`.
  - Endpoint: `GET /api/admin/sellers?status=`, `POST /api/admin/sellers/{id}/approve` (Admin-only).
- **Seed:** 2 seller demo (`seller1/seller2`) set sẵn `Approved` để giữ sản phẩm seed hiển thị.

### Frontend
- Admin: trang mới `AdminSellersPage` (`/admin/sellers`, nav "Duyệt seller") — tab lọc Chờ duyệt/Đã duyệt/Tất cả, nút "Duyệt" từng seller.
- Seller: banner ⏳ "chờ Admin duyệt" trên `SellerDashboardPage` + `SellerProductsPage` khi `sellerStatus === "Pending"`; nút "Thêm sản phẩm" bị disable đến khi duyệt.

### File
- `src/ECommerce.Domain/Enums/SellerStatus.cs`, `Entities/User.cs`
- `Persistence/Configurations/CatalogConfigurations.cs` (`HasConversion<int>`), `DbSeeder.cs`, migration `20260703133450_AddSellerApprovalStatus`
- `Application/Services/AuthService.cs`, `ProductService.cs`, `SellerAdminService.cs` (mới), `DTOs/Auth/AuthDtos.cs`, `DTOs/Admin/DashboardDtos.cs`, `Interfaces/IBusinessServices.cs`, `DependencyInjection.cs`
- `API/Controllers/AdminController.cs`
- Client: `AdminSellersPage.tsx` (mới), `SellerDashboardPage.tsx`, `SellerProductsPage.tsx`, `App.tsx`, `Navbar.tsx`, `hooks.ts`, `types.ts`
- Test: `tests/ECommerce.Tests/Unit/SellerOnboardingTests.cs` (8 test)

### Bất biến được test khóa
- Seller Pending không tạo được product (`Forbidden`); Approved tạo được; Admin tạo được.
- Shop Pending ẩn (`NotFound`); Approved hiện.
- `Approve` chuyển Pending→Approved; duyệt lại → `Conflict`.
- Lọc `?status=Pending` chỉ trả seller Pending.

---

## Kiểm chứng mẻ này
- `dotnet build`: 6 project, 0 lỗi, 0 cảnh báo.
- `dotnet test`: **62/62 pass** (49 cũ + 5 split + 8 onboarding).
- `npm run build` (client): sạch.
- Migration `AddSellerApprovalStatus`: cột `SellerStatus int NULL` trên `Users` (không phá dữ liệu cũ).
