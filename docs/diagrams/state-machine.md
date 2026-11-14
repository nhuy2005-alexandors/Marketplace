# State Machine Diagrams — MiniShop

## 1. Vòng đời Đơn hàng (Order)

Quy tắc cài đặt trong `Order.ChangeStatus()` (Domain layer). Mọi chuyển trạng thái ngoài các cạnh dưới đây bị từ chối (`InvalidOrderTransitionException` → HTTP 409).

```mermaid
stateDiagram-v2
    [*] --> Pending: Checkout (tạo đơn)

    Pending --> Paid: Thanh toán thành công
    Pending --> Cancelled: Khách hủy

    Paid --> Shipped: Admin xác nhận giao
    Paid --> Cancelled: Admin hoàn tất Refund / COD chưa thu tiền hủy trước fulfillment

    Shipped --> Delivered: Giao thành công

    Delivered --> [*]
    Cancelled --> [*]

    note right of Pending
        Trừ tồn kho khi tạo đơn
    end note
    note right of Cancelled
        Hoàn lại tồn kho
    end note
```

**Bảng chuyển trạng thái hợp lệ:**

| Từ \ Đến | Pending | Paid | Shipped | Delivered | Cancelled |
|----------|:-------:|:----:|:-------:|:---------:|:---------:|
| Pending  | — | ✅ | ❌ | ❌ | ✅ |
| Paid     | ❌ | — | ✅ | ❌ | ✅ |
| Shipped  | ❌ | ❌ | — | ✅ | ❌ |
| Delivered| ❌ | ❌ | ❌ | — | ❌ |
| Cancelled| ❌ | ❌ | ❌ | ❌ | — |

> Delivered và Cancelled là trạng thái cuối (không có cạnh ra).

## 2. Vòng đời Thanh toán (Payment)

Cài đặt trong `PaymentService.InitiateAsync` / `ConfirmAsync` qua `IPaymentProvider` (Mock, COD, MoMo). Có 2 kiểu provider:
- **Tức thời** (Mock, COD): `CreatePaymentAsync` trả `Completed = true` ngay trong `InitiateAsync` → không có bước redirect/callback.
- **Redirect** (MoMo): `CreatePaymentAsync` gọi API MoMo tạo giao dịch, trả `RedirectUrl` (payUrl), payment giữ `Pending` cho tới khi cổng gọi lại `GET /api/payments/momo/callback` (hoặc IPN `POST /api/payments/momo/ipn`) → `ConfirmAsync` gọi `VerifyAsync` (kiểm tra chữ ký HMAC-SHA256) để chốt kết quả.

```mermaid
stateDiagram-v2
    [*] --> Pending: InitiateAsync tạo bản ghi payment

    Pending --> Completed: Tức thời — CreatePaymentAsync trả Completed (Mock/COD)
    Pending --> Completed: Redirect — VerifyAsync thành công (MoMo callback)
    Pending --> Failed: CreatePaymentAsync trả lỗi, hoặc VerifyAsync thất bại (sai chữ ký/cổng từ chối)

    Failed --> Completed: Thử lại thành công (InitiateAsync lại)
    Completed --> Refunded: Admin xác nhận đã chuyển tiền (Refund Completed)
    Completed --> Cancelled: Hủy COD chưa thu tiền trước fulfillment
    Pending --> Cancelled: Hủy đơn trước thanh toán

    Completed --> [*]
    Refunded --> [*]

    note right of Pending
        Redirect provider: payment ở Pending
        trong lúc khách xử lý ở cổng ngoài
    end note
    note right of Completed
        Kích hoạt Order: Pending -> Paid
        (PaymentService.Finalize)
    end note
```

`Refunded` được set khi Admin ghi nhận đã chuyển tiền và cung cấp ExternalReference. `Cancelled` dùng cho COD bị hủy trước khi thu tiền; COD không đủ điều kiện Refund trong MVP.

## 3. Vòng đời Refund

Refund chỉ áp dụng cho đơn thanh toán online đã Completed, trước khi bất kỳ OrderItem nào bắt đầu fulfillment. MVP chỉ hỗ trợ hoàn toàn bộ đơn.

```mermaid
stateDiagram-v2
    [*] --> Pending: Customer gửi yêu cầu hợp lệ
    Pending --> Completed: Admin xác nhận đã chuyển tiền + mã chứng từ
    Pending --> Rejected: Admin từ chối + lý do
    Completed --> [*]
    Rejected --> [*]
```

- Mỗi Order/Payment chỉ có một Refund trong MVP.
- Reject không thay đổi Order, Payment hoặc tồn kho.
- Complete cập nhật Refund, Payment, Order, OrderItems và Product.Stock trong một lần SaveChanges.
- Refund, Order, OrderItem và Product dùng RowVersion để phát hiện xử lý đồng thời.
- COD, partial refund và đơn đã bắt đầu giao không thuộc phạm vi.

## 4. Vòng đời Giao hàng theo Item (OrderItem Fulfillment)

Quy tắc cài đặt trong `OrderItem.ChangeStatus()` (Domain layer) — **độc lập** với máy trạng thái Order ở mục 1. `Order.Status` vẫn chỉ do Admin đổi (vòng đời thanh toán/vận đơn toàn đơn: Pending→Paid→Shipped→Delivered→Cancelled). `FulfillmentStatus` là trạng thái giao hàng **của từng dòng sản phẩm**, do **Seller sở hữu item đó** tự cập nhật qua `PUT /api/seller/orders/items/{itemId}/status` — mỗi seller ship phần của mình trong một đơn trộn nhiều seller mà không phụ thuộc seller khác hay Admin. Chuyển sai cạnh bị từ chối (`InvalidOrderTransitionException` → HTTP 409); item không thuộc seller gọi request → HTTP 403.

```mermaid
stateDiagram-v2
    [*] --> Pending: Checkout (tạo OrderItem)

    Pending --> Shipped: Seller xác nhận giao (item của mình)
    Pending --> Cancelled: Khách hủy đơn (item còn Pending)

    Shipped --> Delivered: Giao thành công

    Delivered --> [*]
    Cancelled --> [*]

    note right of Pending
        Trạng thái mặc định khi tạo OrderItem
    end note
    note right of Cancelled
        Khi Order bị hủy (Order.Cancel),
        mọi item còn Pending -> Cancelled
    end note
```

**Bảng chuyển trạng thái hợp lệ:**

| Từ \ Đến | Pending | Shipped | Delivered | Cancelled |
|----------|:-------:|:-------:|:---------:|:---------:|
| Pending   | — | ✅ | ❌ | ✅ |
| Shipped   | ❌ | — | ✅ | ❌ |
| Delivered | ❌ | ❌ | — | ❌ |
| Cancelled | ❌ | ❌ | ❌ | — |

> Delivered và Cancelled là trạng thái cuối (không có cạnh ra). Đây là máy trạng thái thứ ba của hệ thống, tách biệt hoàn toàn khỏi `Order.Status` (mục 1) và `Payment.Status` (mục 2) — một đơn có thể ở `Order.Status = Paid` trong khi các `OrderItem` của nó vẫn ở nhiều `FulfillmentStatus` khác nhau tùy tiến độ giao hàng của từng seller.
