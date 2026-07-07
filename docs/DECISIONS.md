# Decisions (ADR) — MiniShop

Ghi quyết định kiến trúc quan trọng + LÝ DO. Không xóa cũ, chỉ append.

Nguồn bằng chứng: `docs/SRS.md`, `docs/diagrams/architecture-diagrams.md`, `docs/diagrams/state-machine.md`, `docs/tech_specs/*.md`, `task.md`, cấu trúc `src/`.

---

## ADR-001: Clean Architecture 4 tầng (Domain / Application / Infrastructure / API)
- **Ngày**: ~2026-06-29 (migration `InitialCreate`, SRS v1.0)
- **Bối cảnh**: Backend .NET 9 cần tách nghiệp vụ khỏi hạ tầng (EF Core, JWT, cổng thanh toán) để dễ test và thay thế.
- **Quyết định**: 4 project — `ECommerce.Domain` (entity/enum/rule thuần), `ECommerce.Application` (DTO/service/interface/validator), `ECommerce.Infrastructure` (DbContext/JWT/payment/seed), `ECommerce.API` (controller/middleware/DI/Swagger). Phụ thuộc hướng vào trong: `API → Application → Domain`, `Infrastructure → Application/Domain`; Domain không phụ thuộc gì.
- **Lý do**: Dependency Inversion — Application khai báo interface (`IPaymentProvider`, `IAppDbContext`, `IJwtTokenGenerator`, `IFileStorage`), Infrastructure cài đặt, DI bind ở `API/Program.cs`. Tầng use-case không biết chi tiết EF Core/MoMo → dễ mock, dễ thay adapter (NFR-05).
- **Đánh đổi**: Nhiều project + boilerplate interface hơn kiến trúc 1 tầng; tra một luồng phải nhảy qua nhiều lớp.

## ADR-002: Marketplace đa người bán, một Order chứa item nhiều Seller
- **Ngày**: ~2026-07-02 (migration `AddSellerMarketplace`)
- **Bối cảnh**: Từ shop đơn chuyển sang marketplace 3 vai trò (Customer/Seller/Admin). Một giỏ/đơn có thể trộn sản phẩm nhiều shop.
- **Quyết định**: `OrderItem` lưu snapshot `SellerId` tại thời điểm đặt hàng; giỏ/đơn KHÔNG giới hạn theo một seller. Doanh thu/đơn theo seller lọc qua `OrderItem.SellerId`/`Product.SellerId`.
- **Lý do**: Trải nghiệm mua nhiều shop trong một đơn; snapshot `SellerId` giữ đúng chủ sở hữu kể cả khi sản phẩm đổi chủ sau này.
- **Đánh đổi**: Kế toán phức tạp — phải chia (split) tiền và giảm giá theo seller (xem ADR-006).

## ADR-003: Order state machine ở Domain, chỉ Admin đổi trạng thái toàn đơn
- **Ngày**: ~2026-06-29
- **Bối cảnh**: Vòng đời đơn cần chặn chuyển trạng thái sai (vd Pending→Delivered).
- **Quyết định**: `Order.ChangeStatus()` (Domain) chỉ cho các cạnh hợp lệ: Pending→{Paid,Cancelled}, Paid→{Shipped,Cancelled}, Shipped→Delivered. Delivered/Cancelled là cuối. Chuyển sai → `InvalidOrderTransitionException` → HTTP 409. Trạng thái toàn-đơn chỉ Admin đổi.
- **Lý do**: Đặt luật trong Domain giữ bất biến ở lõi, độc lập controller; 409 phân biệt rõ lỗi nghiệp vụ với lỗi client.
- **Đánh đổi**: Domain phải biết đủ luật chuyển; thêm trạng thái mới phải sửa cả bảng cạnh.

## ADR-004: Per-item FulfillmentStatus — máy trạng thái thứ ba, độc lập Order.Status
- **Ngày**: ~2026-07-02 (migration `AddOrderItemFulfillment`)
- **Bối cảnh**: Đơn trộn nhiều seller — mỗi seller cần ship phần của mình độc lập, không chờ seller khác hay Admin.
- **Quyết định**: `OrderItem.ChangeStatus()` riêng (Pending→{Shipped,Cancelled}, Shipped→Delivered). Seller đổi qua `PUT /api/seller/orders/items/{itemId}/status`, chỉ item `SellerId == currentUserId` (sai chủ → 403; sai cạnh → 409). Tách hoàn toàn khỏi `Order.Status` (vẫn chỉ Admin).
- **Lý do**: Một đơn có thể Order.Status=Paid trong khi các item ở nhiều FulfillmentStatus tùy tiến độ từng seller; tách máy trạng thái tránh coupling giữa các seller.
- **Đánh đổi**: Ba máy trạng thái song song (Order/Payment/OrderItem) → tăng độ phức tạp mô hình.

## ADR-005: Thanh toán đa cổng qua IPaymentProvider (Mock / COD / MoMo)
- **Ngày**: ~2026-06-29..07-02
- **Bối cảnh**: Cần hỗ trợ nhiều phương thức và cổng thật (sandbox) mà không cứng hóa vào service.
- **Quyết định**: Trừu tượng `IPaymentProvider` + `PaymentProviderFactory` resolve theo key (fallback `mock`). Hai kiểu: **tức thời** (Mock/COD trả `Completed=true` ngay trong `InitiateAsync`) và **redirect** (MoMo trả `RedirectUrl`/payUrl, giữ Pending tới khi callback `GET /api/payments/momo/callback` hoặc IPN verify HMAC-SHA256). File: `MoMoProvider.cs`, `MockPaymentProvider.cs`, `PaymentProviderFactory.cs`.
- **Lý do**: Thêm cổng mới = thêm một provider, không đụng `PaymentService`; MoMo dùng test credentials sandbox công khai nên chạy cổng thật ngay.
- **Đánh đổi**: Trên `localhost` IPN server-to-server không tới được → đơn chốt qua redirect callback (kém tin cậy hơn IPN, phụ thuộc khách quay lại). Enum `PaymentMethod` (CreditCard/PayPal/CashOnDelivery/EWallet) và `Refunded` được mô hình hóa sẵn nhưng luồng hoàn tiền chưa trong scope.
- **Ghi chú**: Đặc tả gốc nhắc "VNPay/Stripe" nhưng code hiện tại triển khai **MoMo** (AIO v2 sandbox) — ghi theo bằng chứng.

## ADR-006: Chia tiền theo seller là breakdown kế toán, pro-rate giảm giá + bù dư làm tròn
- **Ngày**: 2026-07-03 (`docs/tech_specs/2026-07-03-batch-features.md` mục 4)
- **Bối cảnh**: `Order.DiscountAmount` là giảm giá toàn đơn, không gắn seller nào; cần breakdown mỗi seller nhận bao nhiêu để đối soát/hiển thị. Chưa có payout gateway thật.
- **Quyết định**: `GET /api/orders/{id}/split` gom `OrderItem` theo `SellerId`; `DiscountShare` = pro-rate theo tỉ trọng subtotal (`DiscountAmount × sellerSubtotal/orderSubtotal`); `NetTotal = max(0, Subtotal − DiscountShare)`. Seller **cuối** nhận phần dư (`DiscountAmount − đã_phân_bổ`) để `Σ DiscountShare == DiscountAmount` chính xác.
- **Lý do**: Chia `Math.Round` không hết (vd chia 100/33.33) gây lệch xu; bù dư ở seller cuối giữ bất biến tổng khớp `Order.Total`. Đây là breakdown, không chuyển tiền — an toàn khi chưa có gateway payout.
- **Đánh đổi**: Phần dư dồn vào seller cuối có thể âm với 3+ seller → phải clamp (xem GOTCHAS). Access giới hạn admin/chủ đơn.

## ADR-007: JWT access ngắn (15m) + refresh token 7 ngày rotate + revoke
- **Ngày**: 2026-07-03 (`hardening-batch2.md` mục 1, migration `AddRefreshTokens`)
- **Bối cảnh**: Trước là access-token đơn `ExpiryMinutes=1440`; hết hạn phải login lại, không revoke được.
- **Quyết định**: Access 15 phút (`ExpiryMinutes=15`); refresh 7 ngày (`RefreshExpiryDays=7`), opaque random 32 byte base64url (RNG crypto) lưu DB (`RefreshToken`: Token unique, ExpiresAt, RevokedAt, `IsActive` computed). **Rotate**: refresh hợp lệ → thu hồi cũ, cấp cặp mới; dùng lại token cũ → `Unauthorized`. `LogoutAsync` thu hồi refresh. FE: axios interceptor gặp 401 tự refresh (dedupe) rồi retry.
- **Lý do**: Access ngắn giảm thiệt hại nếu lộ; rotate + revoke phát hiện replay và cho đăng xuất server-side; UX không phải login lại liên tục.
- **Đánh đổi**: Thêm bảng + vòng đời token, phức tạp interceptor FE; JWT `Secret` dev để trong `appsettings.json` (phải override qua `appsettings.Local.json` khi deploy).

## ADR-008: Seller onboarding — Pending → Admin duyệt (SellerStatus nullable)
- **Ngày**: 2026-07-03 (`batch-features.md` mục 5, migration `AddSellerApprovalStatus`)
- **Bối cảnh**: Trước `register-seller` tạo seller active ngay, ai cũng bán không kiểm soát.
- **Quyết định**: Enum `SellerStatus { Pending, Approved }`, trường `User.SellerStatus` **nullable** (`null`=không phải seller; seller mới=Pending; duyệt=Approved). Gate `ProductService.CreateAsync` chặn seller chưa Approved (`Forbidden`), Admin bỏ qua (SellerStatus null). Shop pending ẩn (`GetSellerShopAsync` chỉ trả Approved). Admin duyệt qua `POST /api/admin/sellers/{id}/approve`.
- **Lý do**: Cột `int?` nullable không phá dữ liệu cũ khi migrate; dùng `null` phân biệt non-seller khỏi seller vừa gọn vừa để Admin (null) đi qua gate tự nhiên.
- **Đánh đổi**: Ba nhánh ý nghĩa của một field nullable dễ nhầm (xem GOTCHAS gate null-actor); FE phải sync `sellerStatus` sau khi Admin duyệt.

## ADR-009: EF Core + SQL Server LocalDB, migrate khi khởi động
- **Ngày**: ~2026-06-29
- **Bối cảnh**: Cần CSDL quan hệ chạy được ngay trên máy dev Windows, không cài server nặng.
- **Quyết định**: EF Core + SQL Server `(localdb)\MSSQLLocalDB`, DB `ECommerceDb`; migration versioned trong `Infrastructure/Persistence/Migrations`; `DbSeeder` seed admin/customer/2 seller Approved + sản phẩm mẫu.
- **Lý do**: LocalDB không cần cài SQL Server đầy đủ; migration giữ schema versioned, reproducible; seed cho demo/test ngay.
- **Đánh đổi**: LocalDB chỉ Windows; migration dùng SQL SqlServer-only (vd `rowversion`) nên không replay verbatim trên provider khác (ảnh hưởng cách viết integration test — xem GOTCHAS).

## ADR-010: Optimistic concurrency bằng RowVersion (Products, Coupons)
- **Ngày**: 2026-07-02 (migration `AddConcurrencyTokens`)
- **Bối cảnh**: Checkout/cancel song song có thể race trên tồn kho và lượt coupon.
- **Quyết định**: Cột `RowVersion` (rowversion, nullable) trên `Products` và `Coupons`; service bắt `DbUpdateConcurrencyException` → trả `Conflict` (409) thay vì crash 500.
- **Lý do**: Optimistic lock tránh oversell/over-redeem khi 2 request đồng thời; 409 để client retry.
- **Đánh đổi**: Client phải xử lý 409 và retry; ban đầu chỉ Checkout bọc try/catch, Cancel bị bỏ sót (xem GOTCHAS).

## ADR-011: Rate limiting built-in .NET (global 100/min + policy "auth" 10/min)
- **Ngày**: 2026-07-03 (`hardening-batch2.md` mục 2)
- **Bối cảnh**: Không giới hạn tần suất → brute-force login, spam API.
- **Quyết định**: `AddRateLimiter`: global fixed-window per-IP 100 req/phút; policy "auth" 10 req/phút/IP gắn `[EnableRateLimiting("auth")]` lên `AuthController`. Vượt → 429. `app.UseRateLimiter()` sau CORS, trước Authentication.
- **Lý do**: Middleware built-in không thêm dependency; policy riêng cho auth chống brute-force login/register/refresh.
- **Đánh đổi**: Fixed-window per-IP có thể chặn nhầm nhiều user sau NAT chung; không phân biệt user đã auth.

## ADR-012: Upload ảnh xác thực bằng magic-byte, suy đuôi từ nội dung
- **Ngày**: 2026-07-03 (`batch-features.md` mục 2)
- **Bối cảnh**: `LocalFileStorage` cũ chỉ kiểm phần mở rộng — spoofable; file độc lưu vào `wwwroot/uploads` phục vụ tĩnh gây rủi ro XSS/thực thi.
- **Quyết định**: Đọc file vào MemoryStream, sniff magic bytes (JPEG/PNG/GIF/WEBP), đuôi lưu **suy từ nội dung** (không tin tên gốc); không phải ảnh → `InvalidOperationException`. Giới hạn 5MB ở cả controller (`[RequestSizeLimit]`) và storage (defense-in-depth); file rỗng bị từ chối.
- **Lý do**: Chống file giả đuôi; đuôi theo nội dung đảm bảo file lưu đúng loại thật.
- **Đánh đổi**: Phải buffer toàn file vào memory (stream có thể forward-only); chỉ nhận 4 định dạng ảnh.

## ADR-013: Email/notification mock qua IEmailSender (best-effort)
- **Ngày**: 2026-07-03 (`hardening-batch2.md` mục 4)
- **Bối cảnh**: Chưa thông báo cho khách/seller khi có sự kiện; email ngoài scope SRS (mock).
- **Quyết định**: `IEmailSender` (Application) + `LoggingEmailSender` (log ILogger). Wire 2 sự kiện best-effort (try/catch nuốt lỗi, không fail nghiệp vụ): checkout → "Xác nhận đơn hàng", approve-seller → "Cửa hàng đã duyệt". Ctor nhận `IEmailSender? = null` + `NullEmailSender` fallback để test cũ vẫn compile.
- **Lý do**: Interface hóa để swap SMTP thật sau; best-effort để lỗi gửi mail không làm hỏng checkout; fallback null giữ tương thích ngược test.
- **Đánh đổi**: Hiện chỉ log, không gửi thật; lỗi gửi bị nuốt lặng (không retry/queue).

## ADR-014: Integration test qua WebApplicationFactory + SQLite in-memory
- **Ngày**: 2026-07-03 (`hardening-batch2.md` mục 3)
- **Bối cảnh**: Chỉ có unit test service; chưa test end-to-end qua HTTP thật (middleware/auth/serialization/DI).
- **Quyết định**: `CustomWebApplicationFactory : WebApplicationFactory<Program>` (dùng `public partial class Program`). DB test: SQLite in-memory (1 connection giữ mở) + `EnsureCreated()` sinh schema, rồi stamp `__EFMigrationsHistory` để `MigrateAsync()` của app thành no-op — **không sửa code production**.
- **Lý do**: InMemory provider ném ở `MigrateAsync`; migration SqlServer-only (`rowversion`) không replay verbatim trên SQLite → EnsureCreated + stamp history là cách chạy được mà không đụng production (hệ quả của ADR-009).
- **Đánh đổi**: Schema test sinh từ model (EnsureCreated) chứ không từ migration → có thể lệch nhẹ với schema SQL Server thật; workaround stamp history hơi tinh vi.
