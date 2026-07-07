# Phân tích bài toán — MiniShop

> Tài liệu phân tích bài toán cho hệ thống marketplace thương mại điện tử MiniShop. Đọc trước SRS (`SRS.md`) — tài liệu này giải thích **tại sao** hệ thống được thiết kế như vậy, còn SRS đặc tả **cái gì** phải làm.

## 1. Bối cảnh & phát biểu bài toán

### 1.1 Bối cảnh
Thương mại điện tử dạng **marketplace** (nhiều người bán trên cùng một nền tảng — như Shopee, Lazada, Tiki) khác với mô hình cửa hàng đơn (single-vendor). Người mua duyệt sản phẩm từ nhiều shop, bỏ chung vào một giỏ, và thanh toán một lần — nhưng đơn hàng đó thực chất phải được **tách và xử lý theo từng người bán**. Bài toán đặt ra là xây dựng một nền tảng như vậy với đầy đủ vòng đời: từ đăng ký người bán, đăng sản phẩm, mua hàng, thanh toán qua cổng thật, tới quản trị.

### 1.2 Phát biểu bài toán
Xây dựng hệ thống marketplace cho phép:
- **Người bán (Seller)** tự đăng ký, chờ duyệt, rồi đăng/quản lý sản phẩm của riêng mình.
- **Người mua (Customer)** duyệt sản phẩm từ nhiều shop, mua trong một đơn trộn nhiều người bán, thanh toán qua cổng thật (MoMo).
- **Quản trị viên (Admin)** kiểm duyệt người bán, quản lý toàn sàn, theo dõi doanh thu.
- Mỗi người bán chỉ thấy và xử lý **phần dữ liệu của mình** (sản phẩm, đơn, doanh thu) — không rò rỉ dữ liệu chéo giữa các shop.

### 1.3 Vấn đề cốt lõi cần giải quyết
| # | Vấn đề | Vì sao khó |
|---|--------|-----------|
| P1 | **Đơn trộn nhiều người bán** | Một đơn chứa sản phẩm từ nhiều shop → cần tách doanh thu, tách trạng thái giao hàng, phân quyền xem theo từng seller. |
| P2 | **Cô lập dữ liệu giữa các seller** | Seller A không được xem/sửa sản phẩm, đơn, doanh thu của Seller B — kể cả khi hai sản phẩm nằm chung một đơn. |
| P3 | **Toàn vẹn tồn kho khi mua đồng thời** | Nhiều khách mua cùng lúc sản phẩm sắp hết → không được bán quá số lượng (oversell). |
| P4 | **Thanh toán qua cổng ngoài** | Cổng thanh toán (MoMo) là hệ thống bất đồng bộ, redirect + callback — phải xác minh chữ ký, chống giả mạo, và idempotent khi callback tới muộn/trùng. |
| P5 | **Kiểm duyệt người bán** | Seller mới đăng ký không được bán ngay — cần trạng thái chờ duyệt và cổng chặn ở tầng nghiệp vụ. |

## 2. Phân tích các bên liên quan (Stakeholders)

| Bên liên quan | Mục tiêu | Quan tâm chính |
|---------------|----------|----------------|
| **Khách vãng lai (Guest)** | Xem hàng trước khi quyết định đăng ký | Duyệt/tìm sản phẩm, xem đánh giá không cần đăng nhập |
| **Customer** | Mua hàng thuận tiện, an toàn | Giỏ hàng đa shop, mã giảm giá, thanh toán tin cậy, theo dõi đơn |
| **Seller** | Bán hàng, quản lý shop của mình | Đăng sản phẩm, xem đơn/doanh thu **của riêng mình**, tự ship phần của mình |
| **Admin** | Vận hành & kiểm soát sàn | Duyệt seller, quản lý toàn sàn, dashboard tổng, đổi trạng thái đơn |
| **Cổng thanh toán (MoMo)** | Xử lý giao dịch | Nhận yêu cầu ký đúng, gọi lại callback/IPN |

## 3. Phân tích nghiệp vụ

### 3.1 Ba máy trạng thái độc lập
Điểm mấu chốt của bài toán marketplace: **một đơn hàng có ba vòng đời tách biệt** (chi tiết cạnh chuyển xem `diagrams/state-machine.md`):

1. **`Order.Status`** (toàn đơn) — Pending → Paid → Shipped → Delivered / Cancelled. Do **Admin** điều khiển ở cấp toàn đơn.
2. **`Payment.Status`** (thanh toán) — Pending → Completed / Failed → Refunded. Do luồng thanh toán điều khiển.
3. **`OrderItem.FulfillmentStatus`** (giao hàng theo dòng) — Pending → Shipped → Delivered / Cancelled. Do **từng Seller** cập nhật cho phần sản phẩm của mình.

→ Một đơn có thể đang `Paid` (toàn đơn) trong khi item của shop A đã `Shipped` còn item shop B vẫn `Pending`. Ba máy trạng thái này giải quyết trực tiếp vấn đề **P1** (đơn trộn nhiều seller).

### 3.2 Cô lập dữ liệu theo Seller (P2)
- `Product.SellerId` — chủ sở hữu sản phẩm.
- `OrderItem.SellerId` — **snapshot** seller tại thời điểm bán (giữ lịch sử dù sản phẩm/seller sau đó đổi).
- Ràng buộc ở **tầng application** (không phải DB constraint): seller chỉ sửa/xóa được sản phẩm có `SellerId == currentUserId`; dashboard/đơn hàng lọc theo `SellerId`. Sai chủ → HTTP 403.

### 3.3 Toàn vẹn tồn kho (P3)
- Trừ kho **nguyên tử** khi checkout; dùng optimistic concurrency (`RowVersion` trên Product) → nếu hai request tranh nhau, request thua nhận `DbUpdateConcurrencyException` → HTTP 409, không oversell.
- Tương tự cho Coupon (`RowVersion`) chống dùng quá số lượt (over-redeem).

### 3.4 Thanh toán qua cổng ngoài (P4)
- Trừu tượng hóa qua `IPaymentProvider` (Strategy + Factory) — Mock/COD hoàn tất tức thì; MoMo redirect thật.
- MoMo: ký HMAC-SHA256 khi tạo giao dịch, verify chữ ký khi callback về → chống giả mạo.
- **Idempotent**: nếu đơn đã `Paid` khi callback tới muộn/trùng, trả kết quả hiện tại, không xử lý lại.

### 3.5 Kiểm duyệt người bán (P5)
- `User.SellerStatus` (nullable): null = không phải seller; seller mới đăng ký = `Pending`; Admin duyệt → `Approved`.
- Cổng chặn ở nghiệp vụ: seller `Pending` không tạo được sản phẩm (403), shop ẩn khỏi trang công khai.

## 4. Phạm vi (Scope)

### 4.1 Trong phạm vi (In-scope)
- Xác thực JWT + refresh token, phân quyền RBAC (Customer/Seller/Admin).
- Duyệt/tìm/lọc/phân trang sản phẩm; giỏ hàng; checkout; mã giảm giá.
- Thanh toán: Mock, COD, **MoMo** (cổng sandbox thật).
- Ba máy trạng thái (order / payment / fulfillment).
- Đánh giá (verified-purchase), wishlist, upload ảnh sản phẩm.
- Dashboard Admin (toàn sàn) & Seller (theo shop).
- Onboarding + kiểm duyệt seller.

### 4.2 Ngoài phạm vi (Out-of-scope)
| Hạng mục | Lý do loại trừ |
|----------|----------------|
| Tích hợp vận chuyển thật (GHN/GHTK, tracking) | Là tích hợp bên thứ ba, ngoài trọng tâm; trạng thái giao hàng mô phỏng bằng đổi trạng thái thủ công. |
| Payout thật cho seller | Mới dừng ở breakdown kế toán (chia doanh thu theo seller), chưa chuyển tiền thật. |
| Hoàn tiền (Refund) | Đã mô hình hóa trong enum `PaymentStatus.Refunded` cho khả năng mở rộng, luồng chưa cài đặt. |
| Đa ngôn ngữ / đa tiền tệ | Demo dùng USD (quy đổi VND khi gọi MoMo). |

## 5. Thách thức kỹ thuật & hướng giải quyết

| Thách thức | Hướng giải quyết |
|------------|------------------|
| Tách nghiệp vụ khỏi hạ tầng để dễ test/thay thế | **Clean Architecture** 4 tầng + Dependency Inversion (Application khai báo interface, Infrastructure cài đặt) — xem `diagrams/architecture-diagrams.md`. |
| Chống oversell/over-redeem khi truy cập đồng thời | Optimistic concurrency token (`RowVersion`) → 409 khi xung đột. |
| Callback thanh toán không đáng tin (tới muộn, trùng, giả mạo) | Verify chữ ký HMAC-SHA256 + xử lý idempotent theo `Order.Status`. |
| Giữ lịch sử đơn khi giá/sản phẩm đổi | Snapshot `ProductName`, `UnitPrice`, `SellerId` vào `OrderItem`. |
| Phiên đăng nhập an toàn nhưng không bắt đăng nhập lại liên tục | Access token ngắn hạn (15p) + refresh token dài hạn (7 ngày, xoay vòng/thu hồi). |

## 6. Tiêu chí thành công
- Người mua hoàn tất được vòng mua hàng end-to-end: duyệt → giỏ → mã giảm → checkout → **thanh toán MoMo cổng thật** → đơn `Paid`.
- Seller chỉ thấy/sửa được dữ liệu của mình; truy cập chéo bị chặn (403).
- Không oversell khi mua đồng thời (kiểm chứng bằng test đồng thời + concurrency token).
- Đủ bộ tài liệu phân tích/thiết kế: SRS, use-case, sequence, activity, state-machine, class/ERD, component/deployment.
