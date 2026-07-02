# Class Diagram & ERD — MiniShop

## 1. Class Diagram (Domain Entities)

```mermaid
classDiagram
    class BaseEntity {
        +int Id
        +DateTime CreatedAt
        +DateTime? UpdatedAt
    }

    class User {
        +string Email
        +string PasswordHash
        +string FullName
        +UserRole Role
        +string? ShopName
    }

    class Category {
        +string Name
        +string? Description
    }

    class Product {
        +string Name
        +string? Description
        +decimal Price
        +int Stock
        +string? ImageUrl
        +int CategoryId
        +int SellerId
        +DecreaseStock(int qty)
    }

    class Cart {
        +int UserId
        +decimal Total
    }

    class CartItem {
        +int CartId
        +int ProductId
        +int Quantity
        +decimal Subtotal
    }

    class Order {
        +int UserId
        +OrderStatus Status
        +string ShippingAddress
        +string? CouponCode
        +decimal DiscountAmount
        +decimal Subtotal
        +decimal Total
        +ChangeStatus(OrderStatus)
        +CanCancel() bool
    }

    class OrderItem {
        +int OrderId
        +int ProductId
        +int SellerId
        +string ProductName
        +decimal UnitPrice
        +int Quantity
        +decimal Subtotal
    }

    class Payment {
        +int OrderId
        +decimal Amount
        +PaymentMethod Method
        +PaymentStatus Status
        +string? TransactionId
        +DateTime? PaidAt
    }

    class Review {
        +int ProductId
        +int UserId
        +int Rating
        +string? Comment
    }

    class WishlistItem {
        +int UserId
        +int ProductId
    }

    class Coupon {
        +string Code
        +DiscountType Type
        +decimal Value
        +decimal MinOrderAmount
        +DateTime? ExpiresAt
        +int? MaxUses
        +int TimesUsed
        +bool IsActive
        +IsValidFor(decimal, DateTime) bool
        +CalculateDiscount(decimal) decimal
        +Redeem()
    }

    BaseEntity <|-- User
    BaseEntity <|-- Category
    BaseEntity <|-- Product
    BaseEntity <|-- Cart
    BaseEntity <|-- CartItem
    BaseEntity <|-- Order
    BaseEntity <|-- OrderItem
    BaseEntity <|-- Payment
    BaseEntity <|-- Review
    BaseEntity <|-- WishlistItem
    BaseEntity <|-- Coupon

    %% Coupon: khong co FK/association den entity khac.
    %% Order chi luu CouponCode (string snapshot), khong phai FK.

    User "1" --> "0..1" Cart
    User "1" --> "*" Order
    User "1" --> "*" Review
    User "1" --> "*" WishlistItem
    User "1" --> "*" Product : sells (Seller)
    Category "1" --> "*" Product
    Cart "1" --> "*" CartItem
    CartItem "*" --> "1" Product
    Order "1" --> "*" OrderItem
    Order "1" --> "0..1" Payment
    OrderItem "*" --> "1" Product
    Product "1" --> "*" Review
    WishlistItem "*" --> "1" Product
```

> `User.Products` là navigation phía Seller (một User có `Role = Seller` sở hữu nhiều Product qua `Product.SellerId`). `OrderItem.SellerId` không phải navigation, chỉ là snapshot int (không có association tới User trong sơ đồ) — giữ lại seller tại thời điểm bán dù sản phẩm/seller sau đó đổi.

## 2. Entity Relationship Diagram (ERD)

```mermaid
erDiagram
    USERS ||--o| CARTS : has
    USERS ||--o{ ORDERS : places
    USERS ||--o{ REVIEWS : writes
    USERS ||--o{ WISHLIST_ITEMS : saves
    USERS ||--o{ PRODUCTS : sells
    CATEGORIES ||--o{ PRODUCTS : contains
    CARTS ||--o{ CART_ITEMS : holds
    PRODUCTS ||--o{ CART_ITEMS : in
    ORDERS ||--o{ ORDER_ITEMS : contains
    ORDERS ||--o| PAYMENTS : paid_by
    PRODUCTS ||--o{ ORDER_ITEMS : ordered
    PRODUCTS ||--o{ REVIEWS : reviewed
    PRODUCTS ||--o{ WISHLIST_ITEMS : wished

    USERS {
        int Id PK
        string Email UK
        string PasswordHash
        string FullName
        int Role
        string ShopName "nullable, chỉ dùng khi Role = Seller"
    }
    CATEGORIES {
        int Id PK
        string Name UK
        string Description
    }
    PRODUCTS {
        int Id PK
        string Name
        decimal Price
        int Stock
        string ImageUrl
        int CategoryId FK
        int SellerId FK
    }
    CARTS {
        int Id PK
        int UserId FK
    }
    CART_ITEMS {
        int Id PK
        int CartId FK
        int ProductId FK
        int Quantity
    }
    ORDERS {
        int Id PK
        int UserId FK
        int Status
        string ShippingAddress
        string CouponCode
        decimal DiscountAmount
        decimal Subtotal "computed"
        decimal Total "computed"
    }
    ORDER_ITEMS {
        int Id PK
        int OrderId FK
        int ProductId FK
        int SellerId "snapshot, khong phai navigation FK"
        string ProductName
        decimal UnitPrice
        int Quantity
    }
    PAYMENTS {
        int Id PK
        int OrderId FK
        decimal Amount
        int Method
        int Status
        string TransactionId
    }
    REVIEWS {
        int Id PK
        int ProductId FK
        int UserId FK
        int Rating
        string Comment
    }
    WISHLIST_ITEMS {
        int Id PK
        int UserId FK
        int ProductId FK
    }
    COUPONS {
        int Id PK
        string Code UK
        int Type
        decimal Value
        decimal MinOrderAmount
        datetime ExpiresAt
        int MaxUses
        int TimesUsed
        bool IsActive
    }
```

> `Coupon` không có FK/quan hệ tới bảng khác — `Orders.CouponCode` chỉ là snapshot string, không tham chiếu khóa ngoại tới `Coupons.Code`.

## 3. Ràng buộc & index quan trọng
- `Users.Email` — unique index.
- `Categories.Name` — unique index.
- `CartItems(CartId, ProductId)` — unique (mỗi SP một dòng/giỏ).
- `Reviews(ProductId, UserId)` — unique (một đánh giá/SP/khách).
- `WishlistItems(UserId, ProductId)` — unique.
- `OrderItems` lưu **snapshot** `ProductName` + `UnitPrice` để giữ lịch sử khi giá/sản phẩm đổi.
- Xóa Category bị chặn nếu còn Product (DeleteBehavior.Restrict).
- `Coupons.Code` — unique index.
- `Orders.CouponCode` là snapshot string, không phải FK tới `Coupons`.
- `Orders.Subtotal` / `Orders.Total` là computed property (không lưu cột riêng): `Subtotal = Σ Items.Subtotal`, `Total = Max(0, Subtotal - DiscountAmount)`.
- `Products.SellerId` — FK tới `Users`, có index (phục vụ lọc theo `sellerId` trong search + truy vấn dashboard/orders theo seller).
- `OrderItems.SellerId` — không phải FK (chỉ là snapshot int, không có navigation tới `Users`), nhưng có index để truy vấn nhanh "đơn/doanh thu của seller X" (`DashboardService`, `SellerOrderService`).
- **Ràng buộc ở tầng application (không phải DB constraint):** một Seller chỉ được sửa/xóa sản phẩm có `Product.SellerId == currentUserId`; Admin không bị ràng buộc này. Kiểm tra thực hiện trong `ProductService.UpdateAsync`/`DeleteAsync` (tham số `actorId`, `isAdmin`), không khớp → trả lỗi Forbidden (403).
- Khi tạo sản phẩm (`ProductService.CreateAsync`), `SellerId` luôn được gán bằng id của người gọi (`sellerId` tham số) — client không thể tự chọn seller khác.
