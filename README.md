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
├── docs/                        # SRS, báo cáo, phân tích bài toán
└── .github/workflows/ci.yml     # GitHub Actions: backend, frontend và opt-in Windows LocalDB checks
```

## Chức năng nghiệp vụ

Đăng ký/đăng nhập khách + **đăng ký người bán (seller)** · RBAC Customer/Seller/Admin · tìm kiếm và lọc sản phẩm · giỏ hàng · checkout · coupon · thanh toán mock/COD/MoMo sandbox · state machine đơn hàng · refund MVP · verified-purchase reviews · wishlist · marketplace nhiều seller · dashboard · structured catalog (SKU, Brand, typed attributes).

## Yêu cầu

- .NET 9 SDK và dotnet-ef
- Node 22+
- SQL Server LocalDB (sqllocaldb)

## Chạy backend an toàn

**Không dùng** instance mặc định (localdb)\MSSQLLocalDB hoặc database ECommerceDb cho môi trường Development. Đây là target legacy; runtime và EF design-time đều chặn target này.

Development chỉ chấp nhận một LocalDB instance được đặt tên riêng và database ECommerceDb_Dev.

PowerShell, chạy một lần để tạo instance:

```powershell
sqllocaldb create MarketplaceDev
sqllocaldb start MarketplaceDev
```

Cấu hình connection string trong src/ECommerce.API/appsettings.Local.json (gitignored) hoặc biến môi trường:

```powershell
$env:ConnectionStrings__DefaultConnection = "Server=(localdb)\MarketplaceDev;Database=ECommerceDb_Dev;Integrated Security=true;TrustServerCertificate=true"
```

Áp dụng migrations một cách tường minh vào đúng database cô lập:

```powershell
dotnet ef database update --project src/ECommerce.Infrastructure --startup-project src/ECommerce.API -- --connection "$env:ConnectionStrings__DefaultConnection"
```

Sau đó chạy API:

```powershell
dotnet run --project src/ECommerce.API
```

API **không tự động migrate hoặc seed khi startup**. Không chạy seeder, import, reset hoặc restore lên database gốc. Chỉ dùng database clone/disposable đã được xác định rõ.

## Chạy frontend

```powershell
cd client
npm ci
npm run dev   # http://localhost:5173; proxy API -> :5215
```

## Kiểm thử

```powershell
dotnet test ECommerce.sln
cd client
npm ci
npm test
npm run build
```

SQL Server facts là opt-in và chỉ được chạy trên disposable Windows LocalDB instances. Acceptance receipts ghi rõ test nào chạy trong CI và test nào được thực thi riêng; không dùng database phát triển bền vững.

## Thanh toán và currency boundary

- Mock payment chỉ dành cho Development, được đánh dấu IsDemo và không tính doanh thu/refund/review thật.
- COD tạo trạng thái Accepted / Payment Pending; giao hàng không tự chứng minh đã thu tiền. COD cash collection chưa được triển khai.
- MoMo AIO v2 sandbox có HMAC callback verification. Initiation **fail-closed** cho tới khi Owner cấu hình tường minh Payment:MoMo:CatalogCurrency và Payment:MoMo:CatalogToVndRate.
- UsdToVndRate là legacy setting và bị bỏ qua. Không suy luận catalog currency hoặc FX từ dữ liệu demo USD. Catalog currency hiện vẫn OPEN; không bật MoMo thật trước khi có quyết định được Owner chấp thuận.
- Redirect URL của MoMo lấy từ server configuration; client không được chọn callback destination.

## Tài khoản demo và dữ liệu

Các tài khoản/coupon demo chỉ tồn tại trong database đã được seed trước. API startup không tự tạo tài khoản hoặc dữ liệu mẫu. Không giả định một database mới đã có các tài khoản này.

## CI

Workflow chạy backend build/test, frontend type-check/build/regression tests và các opt-in SQL Server LocalDB migration tests. SQL tests chỉ sử dụng các instance/database disposable có tên rõ ràng.

## Tài liệu

Xem docs/specs/ và docs/acceptance/ để biết business contracts, giới hạn đã công bố và evidence theo từng checkpoint.