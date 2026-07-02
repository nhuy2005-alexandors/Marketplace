# State Machine Diagrams — MiniShop

## 1. Vòng đời Đơn hàng (Order)

Quy tắc cài đặt trong `Order.ChangeStatus()` (Domain layer). Mọi chuyển trạng thái ngoài các cạnh dưới đây bị từ chối (`InvalidOrderTransitionException` → HTTP 409).

```mermaid
stateDiagram-v2
    [*] --> Pending: Checkout (tạo đơn)

    Pending --> Paid: Thanh toán thành công
    Pending --> Cancelled: Khách hủy

    Paid --> Shipped: Admin xác nhận giao
    Paid --> Cancelled: Khách hủy (hoàn kho)

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

Cài đặt trong `PaymentService.InitiateAsync` / `ConfirmAsync` qua `IPaymentProvider` (Mock, COD, VNPay, Stripe). Có 2 kiểu provider:
- **Tức thời** (Mock, COD): `CreatePaymentAsync` trả `Completed = true` ngay trong `InitiateAsync` → không có bước redirect/callback.
- **Redirect** (VNPay, Stripe): `CreatePaymentAsync` trả `RedirectUrl`, payment giữ `Pending` cho tới khi cổng gọi lại `GET /api/payments/{provider}/callback` → `ConfirmAsync` gọi `VerifyAsync` (kiểm tra HMAC-SHA512 với VNPay, hoặc `session_id`/`PaymentStatus` với Stripe) để chốt kết quả.

```mermaid
stateDiagram-v2
    [*] --> Pending: InitiateAsync tạo bản ghi payment

    Pending --> Completed: Tức thời — CreatePaymentAsync trả Completed (Mock/COD)
    Pending --> Completed: Redirect — VerifyAsync thành công (VNPay/Stripe callback)
    Pending --> Failed: CreatePaymentAsync trả lỗi, hoặc VerifyAsync thất bại (sai HMAC/cổng từ chối)

    Failed --> Completed: Thử lại thành công (InitiateAsync lại)
    Completed --> Refunded: Hoàn tiền (tương lai)

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

> `Refunded` được mô hình hóa trong enum cho khả năng mở rộng; luồng hoàn tiền chưa nằm trong phạm vi hiện tại.
