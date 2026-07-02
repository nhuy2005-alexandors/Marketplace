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

Hệ thống hỗ trợ hai vai trò: **Customer** và **Admin**. Thanh toán dùng cổng giả lập (mock). Email/thông báo không nằm trong phạm vi (mock).

### 1.3 Định nghĩa & thuật ngữ (Glossary)
| Thuật ngữ | Ý nghĩa |
|-----------|---------|
| Customer | Khách hàng — mua sắm, đặt hàng |
| Admin | Quản trị viên — quản lý sản phẩm, đơn, xem báo cáo |
| Cart | Giỏ hàng tạm của một khách |
| Order | Đơn hàng chốt từ giỏ |
| Order Status | Trạng thái vòng đời đơn (Pending/Paid/Shipped/Delivered/Cancelled) |
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
- **Customer:** + giỏ hàng, đặt hàng, thanh toán, xem/hủy đơn, đánh giá (đã mua), wishlist.
- **Admin:** + CRUD sản phẩm/danh mục, đổi trạng thái đơn, xem dashboard.

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
| FR-02 | Phân quyền RBAC | Phân tách quyền Customer/Admin qua policy + role claim | Hệ thống |
| FR-03 | Tìm kiếm / lọc / phân trang sản phẩm | Theo từ khóa, danh mục, khoảng giá; sắp xếp; phân trang | Mọi người |
| FR-04 | Giỏ hàng | Thêm/sửa/xóa item, kiểm tra tồn kho, tính tổng | Customer |
| FR-05 | Đặt hàng (Checkout) | Chuyển giỏ → đơn, trừ tồn kho nguyên tử | Customer |
| FR-06 | Thanh toán đa cổng | Mock / COD / VNPay sandbox / Stripe Checkout — redirect + callback verify | Customer |
| FR-07 | Vòng đời đơn hàng | State machine, đổi trạng thái hợp lệ, chặn chuyển sai | Customer/Admin |
| FR-08 | Đánh giá sản phẩm | Rating 1–5 + nhận xét, chỉ khách đã mua, 1 lần/sp | Customer |
| FR-09 | Wishlist | Thêm/xóa sản phẩm yêu thích | Customer |
| FR-10 | Dashboard quản trị | Doanh thu, số đơn theo trạng thái, top sản phẩm | Admin |
| FR-11 | CRUD sản phẩm/danh mục | Quản lý danh mục, sản phẩm + upload ảnh | Admin |
| FR-12 | Mã giảm giá (Coupon) | Percentage/Fixed, đơn tối thiểu, hạn dùng, giới hạn lượt | Customer/Admin |
| FR-13 | Phân trang đơn hàng | Danh sách đơn (khách & admin) chia trang, tổng số trang | Customer/Admin |

### 3.1 Chi tiết một số quy tắc
- **FR-05:** Checkout thất bại nếu giỏ rỗng hoặc tồn kho không đủ; khi đủ, tồn kho giảm và giỏ được làm rỗng.
- **FR-06:** `IPaymentProvider` trừu tượng hóa cổng — VNPay/Stripe trả `RedirectUrl` (chuyển hướng khách sang cổng), callback xác minh chữ ký/session rồi chốt `Order → Paid`. Mock/COD hoàn tất tức thì. Chưa cấu hình key → chạy chế độ demo (không lỗi, hoàn tất ngay).
- **FR-07:** Chuyển trạng thái chỉ theo các cạnh hợp lệ (xem `state-machine.md`); chuyển sai trả HTTP 409.
- **FR-08:** Đánh giá yêu cầu tồn tại đơn (khác Cancelled) chứa sản phẩm đó; trùng đánh giá trả 409.
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
REST/JSON. Nhóm endpoint chính: `/api/auth`, `/api/products`, `/api/categories`, `/api/cart`, `/api/orders`, `/api/wishlist`, `/api/admin`. Tài liệu tương tác qua Swagger UI (`/swagger`).

### 5.2 Người dùng
SPA React: trang sản phẩm, chi tiết, giỏ, thanh toán, đơn hàng, wishlist, dashboard admin.

### 5.3 Dữ liệu
SQL Server qua EF Core; migration `InitialCreate`; seed admin + customer demo + sản phẩm mẫu.

---

## 6. Tài khoản demo
| Vai trò | Email | Mật khẩu |
|---------|-------|----------|
| Admin | admin@shop.com | Admin@123 |
| Customer | user@shop.com | User@123 |
