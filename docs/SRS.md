# Software Requirements Specification (SRS)
## Nền tảng Thương mại điện tử tối giản — "MiniShop"

**Phiên bản:** 1.0
**Ngày:** 2026-06-29

---

## 1. Giới thiệu

### 1.1 Mục đích
Tài liệu này đặc tả yêu cầu phần mềm cho **MiniShop**, một nền tảng thương mại điện tử tối giản nhưng đầy đủ luồng nghiệp vụ cốt lõi: duyệt sản phẩm, giỏ hàng, đặt hàng, thanh toán, quản lý đơn hàng và quản trị. Tài liệu dành cho lập trình viên, kiểm thử viên và người đánh giá.

### 1.2 Phạm vi
MiniShop gồm:
- **REST API** (ASP.NET Core 9 + EF Core) cho toàn bộ nghiệp vụ.
- **SPA frontend** (React + Vite + TypeScript) cho khách hàng và quản trị viên.
- **CSDL** SQL Server.

MiniShop hiện là **marketplace đa người bán (multi-seller)**: hỗ trợ ba vai trò xác thực **Customer**, **Seller** và **Admin** (cộng vai trò Khách vãng lai). Thanh toán dùng cổng giả lập (mock). Email/thông báo không nằm trong phạm vi (mock).

### 1.3 Định nghĩa & thuật ngữ (Glossary)
| Thuật ngữ | Ý nghĩa |
|-----------|---------|
| Customer | Khách hàng — mua sắm, đặt hàng |
| Seller | Người bán — tự đăng ký shop, quản lý sản phẩm và xem báo cáo/đơn của riêng mình |
| Admin | Quản trị viên — quản lý toàn hệ thống, đổi trạng thái đơn, xem dashboard toàn hệ thống |
| ShopName | Tên shop của một Seller (thuộc tính trên `User`) |
| Cart | Giỏ hàng tạm của một khách |
| Order | Đơn hàng chốt từ giỏ — có thể chứa sản phẩm của **nhiều Seller khác nhau** |
| Order Status | Trạng thái vòng đời đơn (Pending/Paid/Shipped/Delivered/Cancelled) — chỉ Admin thay đổi |
| OrderItem.SellerId | Snapshot seller sở hữu sản phẩm tại thời điểm đặt hàng cho dòng item đó |
| Payment | Bản ghi thanh toán gắn với đơn |
| Verified purchase | Khách đã mua sản phẩm — điều kiện để đánh giá |
| JWT | JSON Web Token dùng xác thực |
| RBAC | Role-Based Access Control |

### 1.4 Tài liệu liên quan
- `use-cases.md` — sơ đồ và đặc tả use case
- `sequence-diagrams.md` — sơ đồ tuần tự
- `activity-diagrams.md` — sơ đồ hoạt động
- `state-machine.md` — sơ đồ máy trạng thái
- `class-erd.md` — sơ đồ lớp / ERD

---

## 2. Mô tả tổng quan

### 2.1 Bối cảnh sản phẩm
Kiến trúc 4 lớp (Clean Architecture):
- **Domain** — thực thể, enum, quy tắc nghiệp vụ (state machine đơn hàng).
- **Application** — DTO, service nghiệp vụ, validator, interface.
- **Infrastructure** — EF Core DbContext, JWT, mock payment, seed.
- **API** — controller, middleware, DI, Swagger.

### 2.2 Vai trò người dùng
- **Khách (chưa đăng nhập):** xem/tìm sản phẩm, xem chi tiết & đánh giá.
- **Customer:** + giỏ hàng, đặt hàng, thanh toán, xem/hủy đơn, đánh giá (đã mua), wishlist. Giỏ/đơn của Customer không bị giới hạn theo một seller — có thể mua sản phẩm của nhiều shop trong cùng một đơn.
- **Seller:** tự đăng ký tài khoản (`register-seller`, kèm `ShopName`) — không cần Admin duyệt. + CRUD sản phẩm **của riêng mình** (tạo/sửa/xóa/upload ảnh), xem dashboard và danh sách đơn hàng **chỉ phần liên quan tới sản phẩm của mình** (doanh thu, số liệu, item trong đơn). Seller không đổi trạng thái đơn.
- **Admin:** + CRUD sản phẩm/danh mục (mọi seller), đổi trạng thái đơn, xem dashboard toàn hệ thống.

### 2.3 Ràng buộc
- Backend C# (.NET 9), CSDL SQL Server.
- Xác thực JWT Bearer; mật khẩu hash BCrypt.
- Frontend gọi API qua HTTP/JSON.

---

## 3. Yêu cầu chức năng (Functional Requirements)

> 9+ chức năng nghiệp vụ (không tính CRUD thuần).

| ID | Chức năng | Mô tả | Vai trò |
|----|-----------|-------|---------|
| FR-01 | Đăng ký / Đăng nhập | Tạo tài khoản, đăng nhập nhận JWT; mật khẩu hash BCrypt | Khách |
| FR-02 | Phân quyền RBAC | Phân tách quyền Customer/Seller/Admin qua policy + role claim | Hệ thống |
| FR-03 | Tìm kiếm / lọc / phân trang sản phẩm | Theo từ khóa, danh mục, khoảng giá, `sellerId`; sắp xếp; phân trang | Mọi người |
| FR-04 | Giỏ hàng | Thêm/sửa/xóa item, kiểm tra tồn kho, tính tổng | Customer |
| FR-05 | Đặt hàng (Checkout) | Chuyển giỏ → đơn, trừ tồn kho nguyên tử; một đơn có thể gồm sản phẩm của nhiều seller | Customer |
| FR-06 | Thanh toán đa cổng | Mock / COD / VNPay sandbox / Stripe Checkout — redirect + callback verify | Customer |
| FR-07 | Vòng đời đơn hàng | State machine, đổi trạng thái hợp lệ, chặn chuyển sai; chỉ Admin đổi trạng thái (whole-order, Seller không có quyền này) | Customer/Admin |
| FR-08 | Đánh giá sản phẩm | Rating 1–5 + nhận xét, chỉ khách đã mua, 1 lần/sp | Customer |
| FR-09 | Wishlist | Thêm/xóa sản phẩm yêu thích | Customer |
| FR-10 | Dashboard quản trị / seller | Doanh thu, số đơn theo trạng thái, top sản phẩm — Admin xem toàn hệ thống, Seller chỉ xem phần của mình (`GET /api/seller/dashboard`) | Admin/Seller |
| FR-11 | CRUD sản phẩm/danh mục | Quản lý danh mục (Admin); CRUD sản phẩm + upload ảnh (Admin toàn quyền, Seller chỉ trên sản phẩm của chính mình) | Admin/Seller |
| FR-12 | Mã giảm giá (Coupon) | Percentage/Fixed, đơn tối thiểu, hạn dùng, giới hạn lượt | Customer/Admin |
| FR-13 | Phân trang đơn hàng | Danh sách đơn (khách & admin) chia trang, tổng số trang | Customer/Admin |
| FR-14 | Đăng ký Seller | Tự đăng ký tài khoản vai trò Seller kèm `ShopName` (`POST /api/auth/register-seller`), không cần Admin duyệt | Khách |
| FR-15 | Đơn hàng theo Seller | `GET /api/seller/orders` — danh sách đơn có chứa sản phẩm của seller, phân trang; mỗi đơn trả về **chỉ** các `OrderItem` và phần doanh thu thuộc seller đó | Seller |

### 3.1 Chi tiết một số quy tắc
- **FR-05:** Checkout thất bại nếu giỏ rỗng hoặc tồn kho không đủ; khi đủ, tồn kho giảm và giỏ được làm rỗng. Mỗi `OrderItem` lưu snapshot `SellerId` (seller sở hữu sản phẩm tại thời điểm đặt hàng) — giỏ hàng và đơn hàng **không** bị giới hạn theo một seller, một đơn có thể chứa item từ nhiều shop khác nhau.
- **FR-06:** `IPaymentProvider` trừu tượng hóa cổng — VNPay/Stripe trả `RedirectUrl` (chuyển hướng khách sang cổng), callback xác minh chữ ký/session rồi chốt `Order → Paid`. Mock/COD hoàn tất tức thì. Chưa cấu hình key → chạy chế độ demo (không lỗi, hoàn tất ngay).
- **FR-07:** Chuyển trạng thái chỉ theo các cạnh hợp lệ (xem `state-machine.md`); chuyển sai trả HTTP 409. Trạng thái được quản lý ở cấp toàn Order, chỉ Admin thực hiện — kể cả khi đơn có item của nhiều seller, Seller không tự đổi trạng thái phần của mình.
- **FR-08:** Đánh giá yêu cầu tồn tại đơn (khác Cancelled) chứa sản phẩm đó; trùng đánh giá trả 409.
- **FR-11 (Seller ownership):** `POST/PUT/DELETE /api/products` và `upload-image` cho phép role `Admin,Seller`. Với Seller, hệ thống kiểm tra `product.SellerId == currentUserId` trước khi sửa/xóa — không khớp trả 403 Forbidden. Admin bỏ qua kiểm tra này (sửa/xóa sản phẩm của bất kỳ seller nào). Khi tạo sản phẩm, `SellerId` luôn gán bằng id của người tạo (Seller tự tạo cho mình; Admin tạo thì gán chính Admin làm seller).
- **FR-10/FR-15 (Seller scope):** `DashboardService.GetAsync(int? sellerId)` — truyền `null` trả số liệu toàn hệ thống (Admin dùng), truyền id trả số liệu đã lọc theo `OrderItem.SellerId`/`Product.SellerId` của seller đó (doanh thu, số sản phẩm, số đơn liên quan, phân bố trạng thái, top sản phẩm). `SellerOrderService.GetForSellerAsync` trả đơn có chứa item của seller nhưng lọc `Order.Items` chỉ còn item của seller đó và tính lại subtotal/total trên phần này.
- **FR-12:** Coupon áp dụng tại bước checkout; hệ thống validate hạn dùng, đơn tối thiểu, số lượt còn lại trước khi trừ giảm giá vào `Order.DiscountAmount`.
- **Hủy đơn:** Customer hủy được khi đơn ở Pending/Paid; tồn kho được hoàn lại.

---

## 4. Yêu cầu phi chức năng (Non-Functional Requirements)

| ID | Loại | Yêu cầu |
|----|------|---------|
| NFR-01 | Bảo mật | Mật khẩu hash BCrypt; JWT ký HMAC-SHA256; endpoint nhạy cảm yêu cầu xác thực/role |
| NFR-02 | Bảo mật | Validate đầu vào (FluentValidation) tại biên API |
| NFR-03 | Hiệu năng | Truy vấn sản phẩm phân trang; index trên Email, tên SP, khóa ngoại |
| NFR-04 | Khả dụng | API trả lỗi nhất quán dạng `{ "error": "..." }`; middleware bắt lỗi toàn cục |
| NFR-05 | Bảo trì | Kiến trúc phân lớp, phụ thuộc hướng vào trong; service tách interface |
| NFR-06 | Khả chuyển | CORS cho frontend; cấu hình qua appsettings |
| NFR-07 | Kiểm thử | Unit test cho domain/service; có thể chạy `dotnet test` |
| NFR-08 | Usability | Giao diện responsive (Tailwind), tiếng Việt |

---

## 5. Giao diện ngoài

### 5.1 API
REST/JSON. Nhóm endpoint chính: `/api/auth` (gồm `register-seller`), `/api/products`, `/api/categories`, `/api/cart`, `/api/orders`, `/api/wishlist`, `/api/admin`, `/api/seller` (`dashboard`, `orders` — riêng cho vai trò Seller). Tài liệu tương tác qua Swagger UI (`/swagger`).

### 5.2 Người dùng
SPA React: trang sản phẩm, chi tiết, giỏ, thanh toán, đơn hàng, wishlist, dashboard admin.

### 5.3 Dữ liệu
SQL Server qua EF Core; migration `InitialCreate`; seed admin + customer demo + sản phẩm mẫu.

---

## 6. Tài khoản demo
| Vai trò | Email | Mật khẩu | Shop |
|---------|-------|----------|------|
| Admin | admin@shop.com | Admin@123 | — |
| Customer | user@shop.com | User@123 | — |
| Seller | seller1@shop.com | Seller@123 | TechZone |
| Seller | seller2@shop.com | Seller@123 | BookHaven |

> Sản phẩm mẫu được chia giữa hai shop (TechZone / BookHaven) để minh họa dữ liệu đa seller.
