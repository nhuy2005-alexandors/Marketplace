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

## Currency policy

- **USD is the canonical Marketplace catalog currency.** Product prices, cart/order totals, coupon fixed amounts and minimums, Payment.Amount, Refund.Amount, seller splits and dashboard revenue are all USD.
- Percentage coupon values are percentages, not currency amounts. Fixed coupon values and MinOrderAmount are USD.
- MoMo is a provider boundary: Payment.ProviderAmount/ProviderCurrency and reconciliation observations retain the provider-native quote/capture (VND). They never replace the canonical USD Payment.Amount.
- MoMo accepts USD catalog orders only and requires an explicit `Payment:MoMo:CatalogToVndRate` expressed as VND per USD. `CatalogCurrency` is set to `USD`; no FX rate is configured in the repository. The legacy `UsdToVndRate` setting is ignored. Real MoMo initiation therefore remains fail-closed until an FX source/rate is explicitly approved and configured.
- Provider quotes are rounded to whole VND using AwayFromZero and must satisfy MoMo's 1,000–50,000,000 VND bounds. The quote is frozen before provider I/O and callbacks must match it.
- No historical rows are converted. The repository DbSeeder and synthetic Dataset V2 are USD-denominated; legacy records whose original unit cannot be established remain unverified rather than silently converted.
- Refund.Amount remains the full-order USD obligation. The MVP records an external refund reference/note but does not persist or verify the actual external payout amount/currency; Admin must verify the original captured provider amount from the frozen Payment provider quote before confirming an external transfer. No current FX rate is applied to historical captures.
- Mock payment is demo-only and excluded from revenue/refund/review eligibility. COD remains Accepted / Payment Pending until a collection workflow exists. MoMo redirect URL is server-owned.
## Tài khoản demo và dữ liệu

Các tài khoản/coupon demo chỉ tồn tại trong database đã được seed trước. API startup không tự tạo tài khoản hoặc dữ liệu mẫu. Không giả định một database mới đã có các tài khoản này.

## CI

Workflow chạy backend build/test, frontend type-check/build/regression tests và các opt-in SQL Server LocalDB migration tests. SQL tests chỉ sử dụng các instance/database disposable có tên rõ ràng.

## Tài liệu

Xem docs/specs/ và docs/acceptance/ để biết business contracts, giới hạn đã công bố và evidence theo từng checkpoint.