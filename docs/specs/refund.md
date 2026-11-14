# Refund MVP — MiniShop / Marketplace

## Mục tiêu

Bổ sung chức năng hoàn tiền toàn bộ đơn hàng ở mức phù hợp đồ án. Không triển khai payment ledger, return management, partial refund hoặc tích hợp API hoàn tiền tự động của cổng thanh toán.

## Vai trò

- **Customer:** gửi yêu cầu hoàn tiền cho đơn của chính mình và theo dõi trạng thái.
- **Admin:** xem yêu cầu, từ chối hoặc xác nhận đã thực hiện hoàn tiền bên ngoài hệ thống.

## Điều kiện tạo yêu cầu

Customer chỉ được tạo yêu cầu khi đồng thời thỏa mãn:

1. Order thuộc Customer đang đăng nhập.
2. `Order.Status = Paid`.
3. Có Payment và `Payment.Status = Completed`.
4. `Payment.Method != CashOnDelivery`.
5. `Payment.Amount > 0` và bằng `Order.Total`.
6. Đơn có ít nhất một OrderItem và tất cả item còn `FulfillmentStatus.Pending`.
7. Chưa tồn tại Refund cho Order/Payment.

Refund là hoàn tiền **toàn bộ**: `Refund.Amount = Payment.Amount = Order.Total`, tất cả đều là USD canonical. Không cho client tự truyền số tiền.

Với MoMo, `Payment.ProviderAmount`/`ProviderCurrency` giữ nguyên số tiền capture theo provider (VND). Không quy đổi lại theo tỷ giá tại thời điểm refund. Refund MVP hiện chỉ lưu external reference/note, chưa lưu hoặc xác minh amount/currency thực tế của khoản chuyển tiền bên ngoài; Admin phải đối chiếu capture gốc trước khi xác nhận.

COD bị loại khỏi Refund MVP vì hệ thống chưa ghi nhận được sự kiện thu tiền COD thực tế. Đơn COD chưa fulfillment có thể hủy mà không tạo Refund. Hỗ trợ hoàn COD sau thu tiền cần một collection event riêng, ngoài phạm vi này.

## Trạng thái

```text
Pending ── Admin xác nhận đã chuyển tiền ──> Completed
Pending ── Admin từ chối + lý do ─────────> Rejected
```

Completed và Rejected là trạng thái cuối. MVP chỉ cho một Refund trên mỗi Order/Payment; yêu cầu bị từ chối không thể tạo yêu cầu thứ hai cho cùng đơn.

## Quy trình hoàn tất

1. Customer gửi lý do; hệ thống tạo Refund Pending.
2. Admin kiểm tra đơn, payment và lý do.
3. Nếu từ chối, Admin bắt buộc ghi lý do. Order, Payment và tồn kho không thay đổi.
4. Nếu đã thực hiện chuyển tiền ngoài hệ thống, Admin nhập mã giao dịch/chứng từ và xác nhận hoàn tất.
5. Hệ thống kiểm tra lại eligibility và fulfillment state.
6. Một lần `SaveChangesAsync` cập nhật:
   - Refund → Completed, lưu ProcessedAt, ProcessedBy, ExternalReference và Note.
   - Payment → Refunded.
   - Order → Cancelled.
   - Mọi OrderItem Pending → Cancelled.
   - Product.Stock được cộng lại đúng Quantity đã giữ khi checkout.

Nếu bất kỳ kiểm tra nào thất bại, toàn bộ lần lưu bị rollback. Không tự cộng tồn kho cho đơn đã bắt đầu giao hoặc đã giao.

## API

| Method | Endpoint | Role | Mục đích |
|---|---|---|---|
| POST | `/api/refunds` | Customer | Tạo yêu cầu; body: `{ orderId, reason }` |
| GET | `/api/refunds/mine` | Customer | Lịch sử yêu cầu của chính mình |
| GET | `/api/admin/refunds?status=Pending&page=1&pageSize=20` | Admin | Danh sách/lọc/phân trang |
| POST | `/api/admin/refunds/{id}/complete` | Admin | Ghi nhận hoàn tiền; body: `{ externalReference, note }` |
| POST | `/api/admin/refunds/{id}/reject` | Admin | Từ chối; body: `{ reason }` |

## Data model

`Refund` lưu:

- OrderId, PaymentId, Amount, Reason, Status.
- RequestedAt, ProcessedAt, ProcessedBy.
- ExternalReference, Note.
- CreatedAt, UpdatedAt, RowVersion.

Unique indexes trên OrderId và PaymentId ngăn nhiều yêu cầu hoàn toàn bộ cho cùng một đơn/thanh toán. Check constraints bảo vệ Amount > 0 và enum status hợp lệ.

## Concurrency và tính toàn vẹn

- RowVersion trên Refund bảo vệ quyết định Pending → Completed/Rejected khỏi hai Admin xử lý đồng thời.
- RowVersion trên Order và OrderItem ngăn refund/cancel ghi đè thay đổi trạng thái giao hàng.
- RowVersion trên Product bảo vệ tồn kho khi refund cộng lại.
- Các thay đổi được commit trong cùng SaveChanges transaction; concurrency conflict trả HTTP 409.

## Giới hạn được công bố

- Admin thực hiện chuyển tiền qua kênh ngoài hệ thống; ứng dụng chỉ lưu chứng từ tham chiếu và số tiền Refund theo USD canonical. Actual external payout amount/currency chưa được lưu hoặc xác minh. Trạng thái Completed không phải bằng chứng gateway đã xác nhận.
- Không tự động gọi MoMo refund API.
- Không partial refund, không return-after-delivery, không refund cho đơn có item Shipped/Delivered.
- COD không được refund cho đến khi hệ thống có collection event xác thực tiền đã thu.
