# Gotchas — MiniShop

Bẫy đã dính, tích lũy, KHÔNG xóa.

Nguồn: `docs/tech_specs/2026-07-03-hardening-batch2.md` mục 0 (audit-driven fixes), `docs/tech_specs/2026-07-03-batch-features.md`, `task.md`.

---

## Gate seller lọt khi actor null (pattern `is { Role, ... }`)
- **Triệu chứng**: Request tạo product không đủ điều kiện vẫn lọt qua gate trong một số đường vào.
- **Nguyên nhân**: `ProductService.CreateAsync` dùng pattern `actor is { Role: Seller, ... }`. Khi `actor` null, biểu thức trả **false** — nghĩa là "không phải seller chưa duyệt" → nhánh chặn không kích hoạt, create lọt. Field `SellerStatus` nullable 3 nghĩa (null/Pending/Approved) làm logic dễ nhầm.
- **Cách tránh/fix**: Thêm null-check tường minh trước pattern, actor null → `NotFound`. Với field nullable đa nghĩa, luôn xử lý nhánh null riêng, đừng dựa vào pattern-match để suy ra.

## Cancel đơn song song crash 500 (thiếu catch concurrency)
- **Triệu chứng**: 2 request hủy cùng một đơn đồng thời → 1 crash HTTP 500.
- **Nguyên nhân**: `OrderService.CancelAsync` không bắt `DbUpdateConcurrencyException` (RowVersion optimistic lock) như `CheckoutAsync` đã làm → exception lọt ra thành 500.
- **Cách tránh/fix**: Bọc try/catch `DbUpdateConcurrencyException` → trả `Conflict` (409) để client retry. Khi thêm optimistic concurrency, rà **mọi** đường ghi (checkout, cancel, redeem...) chứ không chỉ đường đầu tiên.

## Split discount âm ở seller cuối với 3+ seller
- **Triệu chứng**: `GET /api/orders/{id}/split` trả `DiscountShare` âm cho seller cuối khi đơn có ≥3 seller.
- **Nguyên nhân**: Seller cuối nhận phần dư làm tròn (`DiscountAmount − đã_phân_bổ`) để tổng khớp chính xác; với nhiều seller sai số dồn lại có thể khiến phần dư âm.
- **Cách tránh/fix**: `Math.Clamp(share, 0, sellerSubtotal)` khi tính phần bù dư. Bù dư luôn kèm clamp về khoảng hợp lệ.

## Coupon discount >2 chữ số thập phân lan xuống split
- **Triệu chứng**: Số tiền chia theo seller lệch xu, tổng không khớp `Order.DiscountAmount`.
- **Nguyên nhân**: `Coupon.CalculateDiscount` (nhất là percentage) trả giá trị >2 chữ số thập phân; sai số lan xuống bước pro-rate split.
- **Cách tránh/fix**: Làm tròn **tại nguồn** — `Math.Round(discount, 2, MidpointRounding.AwayFromZero)` trong `Coupon.CalculateDiscount`. Chuẩn hóa tiền tệ ngay chỗ sinh ra, đừng để bước dưới gánh.

## FE sellerStatus cũ (stale) sau khi Admin duyệt
- **Triệu chứng**: Sau khi Admin duyệt seller, seller vẫn thấy banner "chờ duyệt" và nút thêm sản phẩm bị disable — phải logout/login mới hết.
- **Nguyên nhân**: `sellerStatus` lấy từ JWT/auth store lúc login, không tự refetch sau khi trạng thái server đổi.
- **Cách tránh/fix**: Thêm `setUser` vào `store/auth.ts`; `useMe` (`hooks.ts`) tự sync store; seller pages gọi `useMe` để refetch `/auth/me`. State phái sinh từ server (role/status) phải có đường refetch, đừng chỉ đọc một lần lúc login.

## Refresh token dùng lại sau rotate (replay)
- **Triệu chứng**: Nếu không rotate, refresh token cũ vẫn dùng được sau khi đã cấp cặp mới → nguy cơ replay.
- **Nguyên nhân**: Refresh không rotate/revoke thì token cũ sống tới hết hạn 7 ngày.
- **Cách tránh/fix**: `RefreshAsync` thu hồi token cũ (`RevokedAt`) khi cấp cặp mới; token cũ dùng lại → `Unauthorized`. `LogoutAsync` revoke refresh. Bất biến này được test khóa — đừng nới lỏng.

## Axios interceptor refresh loop / gọi refresh nhiều lần
- **Triệu chứng**: Nhiều request 401 đồng thời có thể kích nhiều lần `/auth/refresh` cùng lúc.
- **Nguyên nhân**: Mỗi request tự gọi refresh khi gặp 401 mà không chia sẻ kết quả.
- **Cách tránh/fix**: Interceptor refresh **1 lần**, dedupe qua promise dùng chung; refresh fail → logout. Retry request gốc bằng token mới.

## MoMo IPN không tới trên localhost
- **Triệu chứng**: Trên dev đơn không tự chốt `Paid` qua IPN server-to-server.
- **Nguyên nhân**: MoMo cloud gọi IPN tới `ipnUrl` nhưng không truy cập được `localhost`.
- **Cách tránh/fix**: Trên dev đơn chốt qua **redirect callback** (`GET /api/payments/momo/callback`, trình duyệt khách tự gọi localhost). Deploy public / ngrok thì IPN mới hoạt động và thành đường xác nhận chính. Không kỳ vọng IPN chạy khi test local.

## Upload: tin phần mở rộng tên file (spoofable)
- **Triệu chứng**: File độc (`evil.html`/`shell.svg`) đổi tên `.jpg` lọt vào `wwwroot/uploads` phục vụ tĩnh → rủi ro XSS/thực thi.
- **Nguyên nhân**: `LocalFileStorage` cũ chỉ kiểm phần mở rộng — có thể giả mạo.
- **Cách tránh/fix**: Sniff magic bytes, suy đuôi từ **nội dung** thật (JPEG/PNG/GIF/WEBP); không phải ảnh → từ chối. Giới hạn 5MB ở cả controller và storage; chặn file rỗng. Đừng tin metadata do client cung cấp.

## Integration test: InMemory/SQLite không nuốt migration SqlServer-only
- **Triệu chứng**: `MigrateAsync()` của app ném khi chạy integration test; InMemory provider không dùng được; SQLite replay migration cũng lỗi.
- **Nguyên nhân**: InMemory ném ở `MigrateAsync`; migration chứa SQL SqlServer-only (vd `rowversion`) nên SQLite không replay verbatim.
- **Cách tránh/fix**: SQLite in-memory (giữ 1 connection mở suốt vòng đời factory) + `EnsureCreated()` sinh schema theo model, rồi stamp `__EFMigrationsHistory` để `MigrateAsync()` thành no-op — không sửa code production. Lưu ý schema test sinh từ model có thể lệch nhẹ schema SQL Server thật.

## Email gửi lỗi làm fail nghiệp vụ (nếu không best-effort)
- **Triệu chứng**: Lỗi gửi mail có thể làm hỏng checkout/approve nếu gọi đồng bộ không bọc.
- **Nguyên nhân**: Notification gọi thẳng trong luồng nghiệp vụ.
- **Cách tránh/fix**: Gửi best-effort — try/catch nuốt lỗi, không để fail đơn hàng/duyệt seller. Side-effect phụ (email) không được chặn nghiệp vụ chính.

## OrderService ctor mới phá test cũ (nếu không có fallback)
- **Triệu chứng**: Thêm `IEmailSender` vào ctor `OrderService` làm test `new OrderService(db)` cũ không compile.
- **Nguyên nhân**: Đổi chữ ký ctor bắt buộc.
- **Cách tránh/fix**: Tham số optional `IEmailSender? email = null` + private `NullEmailSender` no-op fallback → test cũ compile, DI vẫn truyền `LoggingEmailSender` thật ở production. Khi mở rộng ctor, dùng optional + null-object để giữ tương thích ngược.

## [TREO] Demo payment callback [AllowAnonymous] chỉ gate IsDevelopment()
- **Triệu chứng**: Callback demo cho phép ẩn danh, chỉ chặn bằng `IsDevelopment()`.
- **Nguyên nhân**: Chưa có signed nonce/secret per-order; nếu cấu hình env sai (chạy Development ở nơi không nên) là lỗ.
- **Cách tránh/fix**: An toàn hiện tại (chỉ Development). Hardening thật cần signed one-time token per-order — ngoài scope các mẻ đã làm, cần xác nhận trước khi triển khai production.
