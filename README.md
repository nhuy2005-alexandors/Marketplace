# MiniShop — Nền tảng Marketplace tối giản

Full-stack e-commerce: **ASP.NET Core 9 + EF Core (SQL Server)** backend, **React + Vite + TypeScript + Tailwind** frontend.

## Kiến trúc

```
ECommerce.sln
├── src/
│   ├── ECommerce.API            # Controllers, middleware, DI, Swagger, JWT, upload ảnh
│   ├── ECommerce.Application    # DTOs, services, validators, interfaces, Result pattern
│   ├── ECommerce.Domain         # Entities, enums, Order state machine, Coupon
│   └── ECommerce.Infrastructure # DbContext, EF config, migrations, JWT/BCrypt, payment providers, storage, seed
├── tests/ECommerce.Tests        # xUnit: state machine, checkout, coupon, payment, review, auth
├── client/                      # React SPA
├── docs/                        # SRS + UML (use-case, sequence, activity, state-machine, class/ERD)
└── .github/workflows/ci.yml     # GitHub Actions: build + test backend/frontend
```

## Chức năng nghiệp vụ
Đăng ký/đăng nhập khách + **đăng ký người bán (seller)** · RBAC 3 vai trò Customer/Seller/Admin ·
tìm-lọc-phân trang sản phẩm (lọc theo cửa hàng) · giỏ hàng · checkout (trừ kho) ·
**mã giảm giá** (percentage/fixed, min-order, hết hạn, giới hạn lượt) ·
**thanh toán đa cổng** (mock, COD, VNPay sandbox, Stripe Checkout — redirect + callback verify) ·
state machine đơn hàng · đánh giá (verified-purchase) · wishlist ·
**marketplace nhiều người bán** (mỗi seller quản sản phẩm/đơn/doanh thu riêng, 1 đơn trộn nhiều seller) ·
phân trang đơn hàng · dashboard (admin toàn sàn / seller theo cửa hàng) · CRUD sản phẩm/danh mục + upload ảnh.

## Vai trò
| Vai trò | Quyền |
|---------|-------|
| **Guest** | Xem/tìm sản phẩm, đọc đánh giá |
| **Customer** | + giỏ hàng, checkout, thanh toán, đánh giá, wishlist, coupon |
| **Seller** | Quản sản phẩm của mình (CRUD + upload ảnh), xem đơn chứa SP của mình, dashboard cửa hàng |
| **Admin** | Quản toàn sàn: mọi sản phẩm, danh mục, đơn, coupon, dashboard tổng |

## Yêu cầu
- .NET 9 SDK, `dotnet-ef`
- Node 18+
- SQL Server LocalDB (`sqllocaldb`) — có sẵn khi cài Visual Studio hoặc SQL Server Express

## Chạy

**Backend** — tự động migrate + seed dữ liệu mẫu khi khởi động, không cần chạy `ef database update` tay:
```bash
# Lần đầu: tạo & start LocalDB
sqllocaldb create MSSQLLocalDB && sqllocaldb start MSSQLLocalDB

# Run API — http://localhost:5215, Swagger tại /swagger
dotnet run --project src/ECommerce.API
```

**Frontend:**
```bash
cd client
npm install
npm run dev   # http://localhost:5173 (proxy /api -> :5215)
```

Mở http://localhost:5173, đăng nhập bằng tài khoản demo bên dưới.

## Test
```bash
dotnet test
```

## Thanh toán — cấu hình cổng thật
Mặc định `vnpay`/`stripe` chạy ở chế độ **demo** (hoàn tất ngay, không cần key) nếu chưa cấu hình.
Để dùng sandbox/thật, set trong `appsettings.json`:

| Cổng | Config | Lấy ở đâu |
|------|--------|-----------|
| VNPay | `Payment:VnPay:TmnCode`, `Payment:VnPay:HashSecret` | Đăng ký merchant sandbox tại vnpay.vn |
| Stripe | `Payment:Stripe:SecretKey` | Dashboard Stripe → Developers → API keys (test mode) |

Method gửi lên API: `mock` | `cod` | `vnpay` | `stripe`.

## Tài khoản demo
| Vai trò | Email | Mật khẩu |
|---------|-------|----------|
| Admin | admin@shop.com | Admin@123 |
| Customer | user@shop.com | User@123 |
| Seller (TechZone) | seller1@shop.com | Seller@123 |
| Seller (BookHaven) | seller2@shop.com | Seller@123 |

Mã giảm giá mẫu: `WELCOME10` (giảm 10%, đơn tối thiểu $50), `SAVE20` (giảm $20, đơn tối thiểu $100).

## CI/CD
`.github/workflows/ci.yml` chạy khi push/PR vào `main`/`master`: build + test backend (.NET), build frontend (tsc + vite).

## Tài liệu
Xem thư mục [`docs/`](docs/): SRS, use case, sequence, activity, state machine, class/ERD (Mermaid — render trực tiếp trên VSCode/GitHub).
