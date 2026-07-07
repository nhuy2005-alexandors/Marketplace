# Payment (đa cổng: Mock / COD / MoMo) — Spec

> Spec hồi tố từ code hiện có.

## Mục tiêu
Cho phép thanh toán một order qua nhiều cổng (Mock demo, Cash-on-Delivery, MoMo sandbox thật) sau lưng một abstraction chung, để thêm cổng mới không phải sửa `PaymentService`.

## Yêu cầu
- [x] `IPaymentProvider` là abstraction chung: `Key` (định danh), `CreatePaymentAsync` (khởi tạo), `VerifyAsync` (xác minh callback) (src/ECommerce.Application/Interfaces/IServices.cs:37-48)
- [x] `PaymentProviderFactory` resolve provider theo key case-insensitive, không tìm thấy thì fallback `"mock"` (src/ECommerce.Infrastructure/Payments/PaymentProviderFactory.cs:9-20)
- [x] 3 provider đăng ký DI: `MockPaymentProvider` (key `mock`), `CodPaymentProvider` (key `cod`), `MoMoProvider` (key `momo`) (src/ECommerce.Infrastructure/DependencyInjection.cs:27-30)
- [x] `POST /api/orders/{id}/pay` [Authorize] khởi tạo thanh toán: validate method hợp lệ, order tồn tại, đúng chủ đơn (403 nếu không), order đang `Pending` (409 nếu không) (src/ECommerce.API/Controllers/OrdersController.cs:46-54; src/ECommerce.Application/Services/PaymentService.cs:22-36)
- [x] Hai kiểu provider: **tức thời** (Mock/COD trả `Completed=true` ngay, order chuyển `Paid` trong cùng request) và **redirect** (MoMo trả `RedirectUrl`/payUrl, order giữ `Pending` tới khi có callback) (src/ECommerce.Application/Interfaces/IServices.cs:42-43; src/ECommerce.Application/Services/PaymentService.cs:58-70)
- [x] `MockPaymentProvider.CreatePaymentAsync`: chặn `Amount <= 0`, trả `Completed=true` với `MOCK-{guid}` (src/ECommerce.Infrastructure/Payments/MockPaymentProvider.cs:10-16); `VerifyAsync` luôn `Success=true` (dùng cho demo callback nếu có) (:18-22)
- [x] `CodPaymentProvider.CreatePaymentAsync`: luôn `Completed=true` với `COD-{orderId}-{guid}`, không chặn amount (src/ECommerce.Infrastructure/Payments/MockPaymentProvider.cs:29-33)
- [x] `MoMoProvider` tích hợp AIO v2 sandbox: `POST` tới `_opt.Endpoint` (default `https://test-payment.momo.vn/v2/gateway/api/create`) với `requestType="payWithMethod"`, chữ ký `HMAC-SHA256` hex lowercase (src/ECommerce.Infrastructure/Payments/MoMoProvider.cs:44,51,137-142; PaymentOptions.cs:15)
- [x] MoMo quy đổi USD → VND: `amountVnd = round(Amount * UsdToVndRate, AwayFromZero)`, clamp tối thiểu 1000 VND, `UsdToVndRate` default `25000` (src/ECommerce.Infrastructure/Payments/MoMoProvider.cs:35-37; PaymentOptions.cs:19)
- [x] MoMo `CreatePaymentAsync`: `resultCode != 0` → lỗi kèm message MoMo trả; thiếu `payUrl` → lỗi; thành công trả `PaymentInitResult(Completed=false, payUrl, requestId, null)` (src/ECommerce.Infrastructure/Payments/MoMoProvider.cs:80-91)
- [x] MoMo `VerifyAsync`: build lại raw signature từ 13 field callback, so `HMAC-SHA256` computed vs `signature` gửi lên (case-insensitive) — sai thì `Success=false, Error="Invalid signature."` (src/ECommerce.Infrastructure/Payments/MoMoProvider.cs:111-119); đúng thì `Success = (resultCode == "0")` (:121-124)
- [x] `GET /api/payments/momo/callback` [AllowAnonymous]: nhận query params đã ký, gọi `ConfirmAsync("momo", ...)`, redirect khách về `{Client:BaseUrl}/orders?payment=success|failed` (src/ECommerce.API/Controllers/PaymentsController.cs:21-30,45-50)
- [x] `POST /api/payments/momo/ipn` [AllowAnonymous]: nhận JSON body server-to-server, gọi `ConfirmAsync` độc lập với redirect, trả `204 NoContent` (src/ECommerce.API/Controllers/PaymentsController.cs:33-43)
- [x] `ConfirmAsync` idempotent: nếu `order.Status == Paid` (đã chốt trước đó, ví dụ callback + IPN cùng về) → trả `Ok` ngay, không finalize lại (src/ECommerce.Application/Services/PaymentService.cs:95-96)
- [x] Finalize: set `Payment.Status=Completed`, `TransactionId`, `PaidAt=UtcNow`, gọi `order.ChangeStatus(Paid)` — sai state-machine → bắt `InvalidOrderTransitionException` trả `Conflict` (src/ECommerce.Application/Services/PaymentService.cs:105-119; src/ECommerce.Domain/Entities/Order.cs:33-42)
- [x] `AllowDemo` fallback: khi MoMo chưa cấu hình đủ key (`PartnerCode`/`AccessKey`/`SecretKey`) và `AllowDemo=true` → hoàn tất giả lập ngay (`MOMO-DEMO-{guid}`) không gọi API thật (src/ECommerce.Infrastructure/Payments/MoMoProvider.cs:30-33,103-108); `AllowDemo` chỉ bật khi `IsDevelopment()`, override cứng ở `Program.cs`, đè lên giá trị trong `appsettings.*.json` (src/ECommerce.API/Program.cs:24-26)

## Ràng buộc
- Method string từ request (`mock`/`cod`/`momo`) dùng để resolve provider (`PaymentProviderFactory`) **và** map riêng sang `PaymentMethod` enum lưu DB (`momo→EWallet`, `cod→CashOnDelivery`, `mock→CreditCard`) — hai mapping độc lập, không tự đồng bộ nếu thêm provider mới (src/ECommerce.Application/Services/PaymentService.cs:24,39,122-128)
- `Amount` luôn lấy từ `order.Total` tại thời điểm gọi, không cho client tự khai giá (src/ECommerce.Application/Services/PaymentService.cs:38)
- MoMo raw signature CREATE phải đúng thứ tự field: `accessKey, amount, extraData, ipnUrl, orderId, orderInfo, partnerCode, redirectUrl, requestId, requestType` — sai thứ tự MoMo trả lỗi ký (src/ECommerce.Infrastructure/Payments/MoMoProvider.cs:47-50)
- MoMo raw signature VERIFY phải đúng thứ tự field: `accessKey, amount, extraData, message, orderId, orderInfo, orderType, partnerCode, payType, requestId, responseTime, resultCode, transId` (src/ECommerce.Infrastructure/Payments/MoMoProvider.cs:111-115)
- `ParseOrderId` (verify) lấy phần trước `-` đầu tiên của `orderId` MoMo trả về (vì `requestId = "{orderId}-{guid}"`) — đổi format `requestId` sẽ vỡ parse này (src/ECommerce.Infrastructure/Payments/MoMoProvider.cs:39,127-135)
- IPN trên `localhost` không tới được từ MoMo sandbox thật → trong dev, chốt đơn phụ thuộc vào redirect callback (kém tin cậy hơn IPN) (docs/DECISIONS.md ADR-005)
- `Order.ChangeStatus` chỉ cho `Pending→Paid`; đơn không ở `Pending` khi callback verify về thì `Finalize` throw → 409, không crash (src/ECommerce.Domain/Entities/Order.cs:25-30,37-39)

## Quyết định
- Strategy + Factory (`IPaymentProvider` + `PaymentProviderFactory`) thay vì switch-case trong service — thêm cổng mới chỉ cần thêm 1 class + 1 dòng DI, không đụng `PaymentService` (docs/DECISIONS.md ADR-005).
- MoMo AIO v2 sandbox dùng test credentials công khai của MoMo nên chạy được ngay không cần đăng ký merchant thật (src/ECommerce.Infrastructure/Payments/MoMoProvider.cs:10).
- `AllowDemo` gate ở `Program.cs` theo `IsDevelopment()`, không đọc trực tiếp từ config lúc chạy — tránh bật demo nhầm ở production dù config sai (src/ECommerce.API/Program.cs:24-26).
- Idempotent theo `order.Status == Paid` (không theo `Payment.Status`) — đơn giản, đủ chặn double-finalize từ callback+IPN trùng (src/ECommerce.Application/Services/PaymentService.cs:95-96).

## Ngoài phạm vi
- Stripe và VNPay đã có trong đặc tả gốc nhưng đã bị gỡ sạch khỏi code — chỉ còn Mock/COD/MoMo (docs/DECISIONS.md ADR-005 ghi chú; task.md dòng "Gỡ sạch Stripe + VNPay"; grep `Stripe|VNPay` trong `src/` không còn kết quả).
- Không có luồng hoàn tiền (refund) dù `PaymentStatus.Refunded` đã khai enum sẵn (src/ECommerce.Domain/Enums/PaymentStatus.cs:8).
- Không lưu lịch sử nhiều lần thử thanh toán cho một order — `Payment` là 1-1 với `Order` (src/ECommerce.Domain/Entities/Payment.cs:8-9), lần init sau ghi đè lần trước.
- Không có webhook/retry queue nếu IPN lỗi tạm thời — lỗi verify chỉ set `Failed` một lần, không tự retry (src/ECommerce.Application/Services/PaymentService.cs:88-93).
