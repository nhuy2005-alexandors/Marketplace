# Sequence Diagrams — MiniShop

Các sơ đồ tuần tự cho luồng nghiệp vụ chính. Lớp tham chiếu: Controller → Service → DbContext/Gateway.

## 1. Đăng nhập (Login)

```mermaid
sequenceDiagram
    actor U as User
    participant FE as React Client
    participant AC as AuthController
    participant AS as AuthService
    participant H as PasswordHasher
    participant J as JwtTokenGenerator
    participant DB as AppDbContext

    U->>FE: Nhập email + mật khẩu
    FE->>AC: POST /api/auth/login
    AC->>AS: LoginAsync(request)
    AS->>DB: FirstOrDefault(email)
    DB-->>AS: User | null
    alt User tồn tại
        AS->>H: Verify(password, hash)
        H-->>AS: true/false
        alt Mật khẩu đúng
            AS->>J: Generate(user)
            J-->>AS: JWT
            AS-->>AC: Result.Ok(AuthResponse)
            AC-->>FE: 200 { token, user }
            FE->>FE: Lưu token (Zustand persist)
        else Sai mật khẩu
            AS-->>AC: Result.Fail(Unauthorized)
            AC-->>FE: 401 { error }
        end
    else Không tồn tại
        AS-->>AC: Result.Fail(Unauthorized)
        AC-->>FE: 401 { error }
    end
```

## 2. Thêm vào giỏ (Add to Cart)

```mermaid
sequenceDiagram
    actor C as Customer
    participant FE as React Client
    participant CC as CartController
    participant CS as CartService
    participant DB as AppDbContext

    C->>FE: Bấm "Thêm vào giỏ"
    FE->>CC: POST /api/cart/items {productId, qty}
    CC->>CS: AddItemAsync(userId, req)
    CS->>DB: Find(Product)
    DB-->>CS: Product
    CS->>DB: Load Cart (create nếu chưa có)
    alt qty + hiện tại <= stock
        CS->>DB: Add/Update CartItem
        CS->>DB: SaveChanges
        CS-->>CC: Result.Ok(CartDto)
        CC-->>FE: 200 Cart
    else Vượt tồn kho
        CS-->>CC: Result.Fail(Validation)
        CC-->>FE: 400 { error }
    end
```

## 3. Đặt hàng (Checkout)

```mermaid
sequenceDiagram
    actor C as Customer
    participant FE as React Client
    participant OC as OrdersController
    participant OS as OrderService
    participant DB as AppDbContext

    C->>FE: Nhập địa chỉ, bấm Đặt hàng
    FE->>OC: POST /api/orders {shippingAddress}
    OC->>OS: CheckoutAsync(userId, req)
    OS->>DB: Load Cart + Items + Products
    alt Giỏ rỗng
        OS-->>OC: Result.Fail(Validation)
        OC-->>FE: 400 "Cart is empty"
    else Có hàng
        loop Mỗi CartItem
            OS->>OS: Product.DecreaseStock(qty)
            OS->>OS: Tạo OrderItem (snapshot giá)
        end
        alt Đủ tồn kho
            OS->>DB: Add Order, Remove CartItems
            OS->>DB: SaveChanges
            OS-->>OC: Result.Ok(OrderDto Pending)
            OC-->>FE: 200 Order
        else Thiếu tồn kho
            OS-->>OC: Result.Fail(Validation)
            OC-->>FE: 400 "Insufficient stock"
        end
    end
```

> Nếu request kèm `CouponCode`, `CheckoutAsync` validate coupon (còn hiệu lực, đủ điều kiện đơn tối thiểu) và set `Order.DiscountAmount` trước khi lưu — không phải một lệnh gọi riêng.

## 4a. Thanh toán tức thời (Mock / COD)

Áp dụng khi `IPaymentProvider.CreatePaymentAsync` trả `Completed = true` ngay (Mock, COD — không cần redirect ra cổng ngoài).

```mermaid
sequenceDiagram
    actor C as Customer
    participant FE as React Client
    participant OC as OrdersController
    participant PS as PaymentService
    participant PP as IPaymentProvider (Mock/COD)
    participant DB as AppDbContext

    C->>FE: Chọn phương thức (mock/cod), bấm Thanh toán
    FE->>OC: POST /api/orders/{id}/pay {method}
    OC->>PS: InitiateAsync(userId, orderId, req)
    PS->>DB: Load Order + Items + Payment
    alt Hợp lệ (chủ đơn, Order.Status = Pending)
        PS->>DB: Payment.Status = Pending (tạo/cập nhật bản ghi)
        PS->>PP: CreatePaymentAsync(context)
        PP-->>PS: PaymentInitResult(Completed = true, txnId)
        PS->>PS: Finalize(): Payment = Completed, Order.ChangeStatus(Paid)
        PS->>DB: SaveChanges
        PS-->>OC: Result.Ok(PayResultDto{requiresRedirect: false, order})
        OC-->>FE: 200 { requiresRedirect: false, order }
    else Không hợp lệ
        PS-->>OC: Result.Fail(Forbidden/Conflict)
        OC-->>FE: 403/409 { error }
    end
```

## 4b. Thanh toán qua redirect (MoMo)

Áp dụng khi `CreatePaymentAsync` trả `RedirectUrl` (payment vẫn `Pending`). Việc xác nhận thật sự diễn ra sau, khi cổng gọi lại callback endpoint.

```mermaid
sequenceDiagram
    actor C as Customer
    participant FE as React Client
    participant OC as OrdersController
    participant PS as PaymentService
    participant PP as IPaymentProvider (MoMo)
    participant GW as Cổng thanh toán MoMo
    participant PC as PaymentsController
    participant DB as AppDbContext

    C->>FE: Chọn phương thức (momo), bấm Thanh toán
    FE->>OC: POST /api/orders/{id}/pay {method, returnUrl}
    OC->>PS: InitiateAsync(userId, orderId, req)
    PS->>DB: Load Order + Items + Payment
    alt Hợp lệ (chủ đơn, Order.Status = Pending)
        PS->>DB: Payment.Status = Pending (tạo/cập nhật bản ghi)
        PS->>PP: CreatePaymentAsync(context)
        PP->>GW: POST /v2/gateway/api/create (ký HMAC-SHA256, amount VND)
        GW-->>PP: { payUrl, resultCode }
        PP-->>PS: PaymentInitResult(Completed = false, RedirectUrl = payUrl, txnId)
        PS->>DB: Payment.TransactionId = txnId, SaveChanges
        PS-->>OC: Result.Ok(PayResultDto{requiresRedirect: true, redirectUrl})
        OC-->>FE: 200 { requiresRedirect: true, redirectUrl }
        FE->>C: Redirect trình duyệt -> payUrl
        C->>GW: Quét QR / nhập thẻ ATM, xác nhận thanh toán
        GW->>C: Redirect về returnUrl kèm query đã ký (HMAC-SHA256)
        C->>PC: GET /api/payments/momo/callback ?params
        PC->>PS: ConfirmAsync(provider, callbackData)
        PS->>PP: VerifyAsync(callbackData)
        alt Verify hợp lệ (đúng chữ ký / session paid)
            PP-->>PS: PaymentVerifyResult(Success = true, orderId, txnId)
            PS->>DB: Load Order + Payment
            PS->>PS: Finalize(): Payment = Completed, Order.ChangeStatus(Paid)
            PS->>DB: SaveChanges
            PS-->>PC: Result.Ok(OrderDto Paid)
            PC-->>C: Redirect Client /orders?payment=success
        else Verify thất bại (sai chữ ký / cổng từ chối)
            PP-->>PS: PaymentVerifyResult(Success = false, error)
            PS->>DB: Payment.Status = Failed
            PS-->>PC: Result.Fail(Conflict)
            PC-->>C: Redirect Client /orders?payment=failed
        end
    else Không hợp lệ (Initiate)
        PS-->>OC: Result.Fail(Forbidden/Conflict)
        OC-->>FE: 403/409 { error }
    end
```
