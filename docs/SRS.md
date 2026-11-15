# Software Requirements Specification (SRS)
## Nền tảng Thương mại điện tử tối giản — "MiniShop"

**Phiên bản:** 1.2
**Ngày:** 2026-11-14

---

## 1. Giới thiệu

### 1.1 Mục đích
Tài liệu này đặc tả yêu cầu phần mềm cho **MiniShop**, một nền tảng thương mại điện tử tối giản nhưng đầy đủ luồng nghiệp vụ cốt lõi: duyệt sản phẩm, giỏ hàng, đặt hàng, thanh toán, quản lý đơn hàng và quản trị. Tài liệu dành cho lập trình viên, kiểm thử viên và người đánh giá.

### 1.2 Phạm vi
MiniShop gồm:
- **REST API** (ASP.NET Core 9 + EF Core) cho toàn bộ nghiệp vụ.
- **SPA frontend** (React + Vite + TypeScript) cho khách hàng và quản trị viên.
- **CSDL** SQL Server.

MiniShop hiện là **marketplace đa người bán (multi-seller)**: hỗ trợ ba vai trò xác thực **Customer**, **Seller** và **Admin** (cộng vai trò Khách vãng lai). Thanh toán hỗ trợ mock demo, COD và MoMo sandbox. COD chưa có cash-collection workflow; MoMo thật fail-closed cho tới khi Owner cấu hình rõ catalog currency và FX quote. Email/thông báo không nằm trong phạm vi.

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
| FulfillmentStatus | Trạng thái giao hàng riêng của từng `OrderItem`; Seller chỉ chuyển Pending→Shipped→Delivered. Cancelled chỉ do luồng hủy/hoàn tiền toàn đơn thực hiện |
| Payment | Bản ghi thanh toán gắn với đơn |
| Verified purchase | Đơn Paid/Shipped/Delivered, Payment Completed, không demo/không reconciliation và có OrderItem của sản phẩm chưa bị hủy |
| Refund | Yêu cầu hoàn tiền toàn bộ đơn đã thanh toán, do Admin xử lý |
| JWT | JSON Web Token dùng xác thực |
| RBAC | Role-Based Access Control |

### 1.4 Tài liệu liên quan
- `diagrams/use-cases.md` — sơ đồ và đặc tả use case
- `diagrams/sequence-diagrams.md` — sơ đồ tuần tự
- `diagrams/activity-diagrams.md` — sơ đồ hoạt động
- `diagrams/state-machine.md` — sơ đồ máy trạng thái
- `diagrams/class-erd.md` — sơ đồ lớp / ERD
- `diagrams/architecture-diagrams.md` — sơ đồ component / deployment

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
- **Seller:** đăng ký (`register-seller`, kèm `ShopName`) ở trạng thái Pending; Admin phải duyệt trước khi Seller được tạo sản phẩm hoặc shop xuất hiện công khai. Seller CRUD sản phẩm của riêng mình, cập nhật fulfillment Shipped/Delivered cho item thuộc shop và xem dashboard/đơn hàng trong phạm vi của mình.
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
| FR-03 | Tìm kiếm / lọc / phân trang sản phẩm | Theo từ khóa, danh mục, seller, brand, khoảng giá, tồn kho và typed structured attributes; sắp xếp; phân trang | Mọi người |
| FR-04 | Giỏ hàng | Thêm/sửa/xóa item, kiểm tra tồn kho, tính tổng | Customer |
| FR-05 | Đặt hàng (Checkout) | Chuyển giỏ → đơn, trừ tồn kho nguyên tử; một đơn có thể gồm sản phẩm của nhiều seller | Customer |
| FR-06 | Thanh toán đa cổng | Mock / COD / MoMo sandbox (AIO v2) — redirect + callback verify | Customer |
| FR-07 | Vòng đời đơn hàng | State machine, đổi trạng thái hợp lệ, chặn chuyển sai; chỉ Admin đổi trạng thái (whole-order, Seller không có quyền này) | Customer/Admin |
| FR-08 | Đánh giá sản phẩm | Rating 1–5 + nhận xét, chỉ khách đã mua, 1 lần/sp | Customer |
| FR-09 | Wishlist | Thêm/xóa sản phẩm yêu thích | Customer |
| FR-10 | Dashboard quản trị / seller | Doanh thu, số đơn theo trạng thái, top sản phẩm — Admin xem toàn hệ thống, Seller chỉ xem phần của mình (`GET /api/seller/dashboard`) | Admin/Seller |
| FR-11 | CRUD catalog | CRUD sản phẩm; quản lý Brand và AttributeDefinition (Admin); Seller chỉ quản lý sản phẩm của mình và nhập typed attributes theo category | Admin/Seller |
| FR-12 | Mã giảm giá (Coupon) | Percentage/Fixed, đơn tối thiểu, hạn dùng, giới hạn lượt | Customer/Admin |
| FR-13 | Phân trang đơn hàng | Danh sách đơn (khách & admin) chia trang, tổng số trang | Customer/Admin |
| FR-14 | Đăng ký Seller | `POST /api/auth/register-seller` tạo Seller ở trạng thái Pending; Admin duyệt trước khi được bán hàng | Khách/Admin |
| FR-15 | Đơn hàng theo Seller | `GET /api/seller/orders` — danh sách đơn có chứa sản phẩm của seller, phân trang; mỗi đơn trả về **chỉ** các `OrderItem` và phần doanh thu thuộc seller đó | Seller |
| FR-16 | Giao hàng theo item (per-item fulfillment) | `PUT /api/seller/orders/items/{itemId}/status` — Seller chỉ đổi Pending→Shipped→Delivered cho item thuộc mình; Cancelled chỉ được thực hiện bởi luồng hủy/hoàn tiền toàn đơn. Chuyển sai cạnh → 409, sai owner → 403 | Seller |
| FR-17 | Tạo danh mục bởi Seller | `POST /api/categories` mở cho role `Admin,Seller` — Seller tạo danh mục mới ngay khi thêm sản phẩm, không cần chờ Admin. Xóa danh mục (`DELETE`) vẫn chỉ Admin | Admin/Seller |
| FR-18 | Structured product catalog | Admin quản lý Brand và AttributeDefinition; Product có SKU, Brand optional và typed attributes theo Category; public search lọc theo Brand/attribute | Admin/Seller/Mọi người |
| FR-18 | Hoàn tiền toàn bộ đơn hàng (Refund MVP) | Customer gửi yêu cầu cho đơn Paid đã thanh toán online, chưa fulfillment; Admin ghi nhận hoàn tiền thủ công hoặc từ chối có lý do; xem `docs/specs/refund.md` | Customer/Admin |

### 3.1 Chi tiết một số quy tắc
- **FR-05:** Checkout thất bại nếu giỏ rỗng hoặc tồn kho không đủ; khi đủ, tồn kho giảm và giỏ được làm rỗng. Mỗi `OrderItem` lưu snapshot `SellerId` (seller sở hữu sản phẩm tại thời điểm đặt hàng) — giỏ hàng và đơn hàng **không** bị giới hạn theo một seller, một đơn có thể chứa item từ nhiều shop khác nhau.
- **FR-06:** `IPaymentProvider` trừu tượng hóa cổng. Mock demo được đánh dấu `IsDemo` và không được tính là doanh thu/verified purchase. COD chuyển Order sang `Accepted`, Payment vẫn `Pending` cho tới khi có collection event; hiện chưa có collection workflow nên COD chưa được tính là doanh thu/verified purchase. MoMo sandbox dùng redirect + callback HMAC-SHA256. Mỗi Order hiện có một payment attempt bất biến; không retry bằng cách ghi đè attempt. MoMo thật fail-closed nếu thiếu catalog currency/FX quote được Owner cấu hình.
- **FR-07:** Chuyển trạng thái chỉ theo các cạnh hợp lệ (xem `diagrams/state-machine.md`); chuyển sai trả HTTP 409. Trạng thái được quản lý ở cấp toàn Order, chỉ Admin thực hiện — kể cả khi đơn có item của nhiều seller, Seller không tự đổi trạng thái phần của mình.
- **FR-08:** Đánh giá yêu cầu Order ở Paid/Shipped/Delivered, Payment Completed, không phải demo, không yêu cầu reconciliation và có OrderItem của sản phẩm chưa Cancelled; COD chỉ đủ điều kiện sau khi có collection event. Mỗi user chỉ đánh giá một lần/sản phẩm; trùng trả 409.
- **FR-11 (Seller ownership):** Product CRUD cho `Admin,Seller`; Seller phải Approved và chỉ sửa/xóa sản phẩm có `SellerId == currentUserId`. SKU được trim/uppercase và unique toàn catalog; nếu không gửi SKU, backend tạo SKU duy nhất. Brand là optional. AttributeDefinition thuộc một Category và có DataType cố định; ProductAttributeValue phải có đúng một typed value phù hợp. Các active required attributes phải được cung cấp khi tạo hoặc khi cập nhật danh sách attributes. CategoryId của Product và CategoryId/DataType của AttributeDefinition bất biến sau khi tạo vì tham gia composite FK.
- **FR-10/FR-15 (Seller scope):** `DashboardService.GetAsync(int? sellerId)` — truyền `null` trả số liệu toàn hệ thống (Admin dùng), truyền id trả số liệu đã lọc theo `OrderItem.SellerId`/`Product.SellerId` của seller đó (doanh thu, số sản phẩm, số đơn liên quan, phân bố trạng thái, top sản phẩm). `SellerOrderService.GetForSellerAsync` trả đơn có chứa item của seller nhưng lọc `Order.Items` chỉ còn item của seller đó và tính lại subtotal/total trên phần này.
- **FR-12:** Coupon áp dụng tại bước checkout; hệ thống validate hạn dùng, đơn tối thiểu, số lượt còn lại trước khi trừ giảm giá vào `Order.DiscountAmount`.
- **FR-10/FR-15 (Pro-rate giảm giá coupon theo seller):** `Order.DiscountAmount` là giảm giá **toàn đơn**, không gắn riêng seller nào. Cả `SellerOrderService.GetForSellerAsync` và `DashboardService.GetAsync(sellerId)` chia tỉ lệ giảm giá này theo tỉ trọng subtotal của seller trong đơn: `sellerDiscount = DiscountAmount × (sellerSubtotal / orderSubtotal)`, sau đó `sellerTotal = sellerSubtotal - sellerDiscount`. Doanh thu hệ thống (`sellerId = null`) dùng trực tiếp `Order.Total` (đã trừ giảm giá) của các đơn đã thanh toán. Ví dụ đã verify: đơn $100 (seller A $60 + seller B $40), coupon giảm $20 → A thấy discount $12/total $48, B thấy discount $8/total $32, tổng $80 khớp `Order.Total` thực nhận.
- **FR-16 (Giao hàng theo item):** `OrderItem.ChangeStatus()` tách biệt `Order.ChangeStatus()`. Seller chỉ chuyển Pending→Shipped→Delivered cho item thuộc mình; Seller không tự hủy item. Khi toàn đơn được hủy hợp lệ, hệ thống chuyển các item còn Pending sang Cancelled. SQL trigger bảo vệ snapshot OrderItem (`OrderId/ProductId/SellerId/ProductName/UnitPrice/Quantity`) sau checkout.
- **FR-18 (Refund MVP):** Customer chỉ được yêu cầu hoàn tiền cho đơn thuộc mình, Order Paid, Payment Completed (không phải COD), Payment.Amount khớp Order.Total và tất cả OrderItem còn Pending. Refund là hoàn toàn bộ đơn, một Refund tối đa trên mỗi Order/Payment. Customer dùng `POST /api/refunds` và `GET /api/refunds/mine`; Admin dùng `GET /api/admin/refunds`, `POST /api/admin/refunds/{id}/complete` hoặc `/reject`. Admin bắt buộc ghi mã giao dịch/chứng từ sau khi chuyển tiền bên ngoài hệ thống; khi Completed, Refund/Payment/Order/OrderItems/tồn kho được cập nhật trong một SaveChanges transaction. Rejected không thay đổi Order/Payment/tồn kho. Không partial refund, COD refund hoặc refund sau khi fulfillment bắt đầu.
- **Hủy đơn:** Customer tự hủy Pending; COD chưa thu tiền có thể hủy trước khi fulfillment bắt đầu, không tạo Refund. Đơn Paid online phải đi qua Refund workflow; Admin không được bỏ qua workflow này bằng cách đổi Order.Status trực tiếp. Tồn kho chỉ hoàn khi mọi item vẫn Pending.

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
| NFR-09 | Database integrity | SQL Server enforces FK/check constraints; checkout snapshots are immutable via trigger; typed attributes use type/category constraints and indexes |

---

## 5. Giao diện ngoài

### 5.1 API
REST/JSON. Nhóm endpoint chính: `/api/auth`, `/api/products` (SKU/Brand/structured attribute filters), `/api/brands`, `/api/catalogattributes`, `/api/categories`, `/api/cart`, `/api/orders`, `/api/refunds`, `/api/wishlist`, `/api/admin`, `/api/seller`. `GET /api/brands` và `GET /api/catalogattributes` công khai; CRUD Brand/AttributeDefinition chỉ Admin. Product search hỗ trợ `brandId` và tối đa 20 `attributeFilters` AND-combined với `ValueText`, `MinValue/MaxValue` hoặc `ValueBoolean`. Tài liệu tương tác qua Swagger UI (`/swagger`).

### 5.2 Người dùng
SPA React: trang sản phẩm, chi tiết, giỏ, thanh toán, đơn hàng, wishlist, dashboard admin.

### 5.3 Dữ liệu
SQL Server qua EF Core; migrations được quản lý theo thứ tự. Owner lane bổ sung FK/check constraints, typed-attribute indexes và SQL trigger bảo vệ checkout snapshot. Migration mới có preflight orphan/enum checks và safety guard theo convention hiện tại của LocalDB `ECommerceDb_Dev`; không seed, import, reset hoặc sửa dữ liệu V2.

---

## 6. Tài khoản demo
| Vai trò | Email | Mật khẩu | Shop |
|---------|-------|----------|------|
| Admin | admin@shop.com | Admin@123 | — |
| Customer | user@shop.com | User@123 | — |
| Seller | seller1@shop.com | Seller@123 | TechZone |
| Seller | seller2@shop.com | Seller@123 | BookHaven |

> Sản phẩm mẫu được chia giữa hai shop (TechZone / BookHaven) để minh họa dữ liệu đa seller.
