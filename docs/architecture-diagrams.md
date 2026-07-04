# Component & Deployment Diagram — MiniShop

Tài liệu mô tả kiến trúc phần mềm (Component) và cách triển khai (Deployment) của hệ thống MiniShop.

## 1. Component Diagram

Backend theo **Clean Architecture** — phụ thuộc hướng vào trong: `API → Application → Domain`, `Infrastructure → Application/Domain`. Domain là lõi thuần, không phụ thuộc gì.

```mermaid
graph TB
    subgraph Client["Frontend — React SPA"]
        UI[UI Pages & Components]
        Store[Zustand Store<br/>auth state]
        Query[TanStack Query<br/>server cache]
        ApiClient[Axios API Client<br/>JWT interceptor]
        UI --> Store
        UI --> Query
        Query --> ApiClient
    end

    subgraph API["ECommerce.API — Presentation"]
        Controllers[Controllers<br/>Auth/Products/Cart/Orders/<br/>Payments/Coupons/Admin/Seller]
        MW[Middleware<br/>JWT Auth · Exception · CORS]
        Validators[FluentValidation]
        Swagger[Swagger/OpenAPI]
    end

    subgraph App["ECommerce.Application — Use Cases"]
        Services[Services<br/>Auth/Product/Order/Payment/<br/>Coupon/Cart/Admin/Seller]
        Interfaces[Interfaces<br/>IPaymentProvider · IAppDbContext<br/>IJwtTokenGenerator · IFileStorage]
        DTOs[DTOs & Mappers]
    end

    subgraph Domain["ECommerce.Domain — Core"]
        Entities[Entities<br/>User/Product/Order/Payment/<br/>Cart/Coupon/Review]
        Enums[Enums<br/>OrderStatus/PaymentMethod/...]
        Rules[Business Rules<br/>Order.ChangeStatus]
    end

    subgraph Infra["ECommerce.Infrastructure — Adapters"]
        DbContext[AppDbContext<br/>EF Core + Configurations]
        MoMo[MoMoProvider<br/>+ Mock/COD providers]
        Jwt[JwtTokenGenerator]
        Storage[LocalFileStorage]
        Seeder[DbSeeder]
    end

    ExtDB[(SQL Server)]
    ExtMoMo[MoMo AIO v2<br/>Sandbox Gateway]

    ApiClient -->|HTTPS REST + JWT| Controllers
    Controllers --> Services
    Controllers --> Validators
    Services --> Interfaces
    Services --> DTOs
    Services --> Entities
    Interfaces -.implemented by.-> DbContext
    Interfaces -.implemented by.-> MoMo
    Interfaces -.implemented by.-> Jwt
    Interfaces -.implemented by.-> Storage
    DbContext --> Entities
    DbContext -->|EF Core| ExtDB
    MoMo -->|HTTPS API + HMAC-SHA256| ExtMoMo
```

**Nguyên tắc phụ thuộc (Dependency Inversion):** `Application` khai báo interface (vd `IPaymentProvider`, `IAppDbContext`), `Infrastructure` cài đặt. DI container ở `API/Program.cs` bind chúng lúc khởi động → tầng use-case không biết chi tiết EF Core hay MoMo, dễ test và thay thế.

## 2. Deployment Diagram

Mô tả các node vật lý/logic khi chạy. Ở môi trường dev, frontend và backend chạy tách port; production gợi ý reverse-proxy + object storage.

```mermaid
graph TB
    subgraph UserDevice["Máy người dùng"]
        Browser[Trình duyệt<br/>React SPA đã build]
        MoMoApp[MoMo Test App<br/>/ QR scanner]
    end

    subgraph WebServer["Web/App Server"]
        Kestrel[Kestrel<br/>ASP.NET Core .NET 9<br/>ECommerce.API]
        StaticFiles[wwwroot/uploads<br/>ảnh sản phẩm]
    end

    subgraph DBServer["Database Server"]
        SqlServer[(SQL Server<br/>EF Core migrations)]
    end

    subgraph MoMoCloud["MoMo Cloud"]
        MoMoGW[MoMo AIO v2<br/>test-payment.momo.vn]
    end

    subgraph CICD["GitHub Actions"]
        CI[ci.yml<br/>build + test]
    end

    Browser -->|HTTPS REST / JWT| Kestrel
    Browser -->|redirect payUrl| MoMoGW
    MoMoApp -->|quét QR / thanh toán| MoMoGW
    MoMoGW -->|redirect callback GET| Kestrel
    MoMoGW -.->|IPN POST server-to-server<br/>chỉ khi API public| Kestrel
    Kestrel -->|TCP 1433| SqlServer
    Kestrel -->|HTTPS create payment| MoMoGW
    Kestrel --> StaticFiles
    CI -.->|deploy artifact| WebServer
```

### Ghi chú triển khai

| Node | Dev (hiện tại) | Production (gợi ý) |
|------|----------------|--------------------|
| Frontend | Vite dev server `:5173` | Build tĩnh, serve qua CDN / reverse-proxy (Nginx) |
| Backend | Kestrel `:5215` | Kestrel sau Nginx/IIS, HTTPS + domain thật |
| Database | SQL Server local | SQL Server managed (Azure SQL) |
| File upload | `wwwroot/uploads` local disk | Object storage (S3 / Azure Blob) |
| MoMo | Sandbox, test credentials công khai | Merchant key thật, API public để nhận IPN |

> **IPN trên dev:** MoMo gọi IPN server-to-server tới `ipnUrl`. Trên `localhost`, MoMo cloud không truy cập được nên IPN không tới — đơn được chốt qua **redirect callback** (trình duyệt khách tự gọi `localhost`). Khi deploy public (hoặc dùng ngrok), IPN mới hoạt động và trở thành đường xác nhận chính (đáng tin hơn redirect vì không phụ thuộc khách quay lại).
