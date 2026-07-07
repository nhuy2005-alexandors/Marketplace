# Cart + Order — Spec

> Spec hồi tố từ code hiện có.

## Mục tiêu
Cho phép buyer quản lý giỏ hàng và checkout thành đơn hàng multi-seller (mỗi `OrderItem` snapshot seller/giá tại thời điểm đặt), theo dõi trạng thái đơn qua state machine, hủy đơn có hoàn tồn kho, và chia tiền đơn theo từng seller khi có coupon.

## Yêu cầu

### Cart
- [x] `GET /api/cart` trả cart hiện tại của user, tự tạo cart rỗng nếu chưa có (`src/ECommerce.API/Controllers/CartController.cs:15-19`, `src/ECommerce.Application/Services/CartService.cs:89-101`)
- [x] `POST /api/cart/items` thêm sản phẩm: `Quantity <= 0` bị chặn (`CartService.cs:23-24`), `ProductId` không tồn tại → 404 (`CartService.cs:25-27`), nếu item đã có trong cart thì cộng dồn số lượng (`CartService.cs:30-31, 41-44`)
- [x] Kiểm tra tồn kho khi thêm: `newQty > product.Stock` → 400 Validation `"Only {Stock} in stock."` (`CartService.cs:32-33`) — chỉ **kiểm tra**, không trừ/giữ tồn kho ở bước này
- [x] `PUT /api/cart/items/{itemId}` cập nhật số lượng; `Quantity <= 0` → xóa item luôn (`CartService.cs:55-58`); ngược lại kiểm tra lại tồn kho (`CartService.cs:61-63`)
- [x] `DELETE /api/cart/items/{itemId}` và `DELETE /api/cart` (clear toàn bộ) (`CartController.cs:37-48`, `CartService.cs:69-87`)
- [x] Item trong `UpdateItemAsync`/`RemoveItemAsync` được tìm trong `cart.Items` của chính user (`LoadCartAsync(userId,...)` trước), nên user A không sửa/xóa được item của cart user B — không tồn tại thì 404 (`CartService.cs:51-54, 72-74`)
- [x] `CartItem.Subtotal` tính từ **giá sản phẩm hiện tại** (`Product.Price`), không snapshot — giá trong cart trôi theo giá catalog cho tới lúc checkout (`src/ECommerce.Domain/Entities/CartItem.cs:15`)

### Checkout (cart → order)
- [x] `POST /api/orders` — cart rỗng hoặc không tồn tại → 400 `"Cart is empty."` (`src/ECommerce.Application/Services/OrderService.cs:35-36`)
- [x] Mỗi `CartItem` sinh một `OrderItem` snapshot `ProductName`, `UnitPrice`, `SellerId`, `ProductId`, `Quantity` tại thời điểm checkout — không tham chiếu giá/seller sau này đổi (`OrderService.cs:50-57`)
- [x] Trừ tồn kho qua `Product.DecreaseStock(quantity)` cho từng item trong cùng vòng lặp trước khi save (`OrderService.cs:49`); vượt tồn kho → `DomainException` bắt lại thành 400 Validation (`Product.cs:25-32`, `OrderService.cs:60-63`)
- [x] Một `Order` có thể chứa `OrderItem` của nhiều seller khác nhau (không tách sub-order), mỗi item giữ `SellerId` riêng (`src/ECommerce.Domain/Entities/OrderItem.cs:15`)
- [x] Coupon (optional): code trim + uppercase, `Coupon.IsValidFor(subtotal, now)` kiểm tra active/hết hạn/hết lượt/`MinOrderAmount`; không hợp lệ → 400 `"Invalid or expired coupon."` (`OrderService.cs:65-71`, `src/ECommerce.Domain/Entities/Coupon.cs:26-33`)
- [x] `DiscountAmount = Coupon.CalculateDiscount(subtotal)` (percentage hoặc fixed, clamp không vượt subtotal, round 2 chữ số) và `Coupon.Redeem()` tăng `TimesUsed` (`OrderService.cs:72-74`, `Coupon.cs:35-46`)
- [x] Toàn bộ thay đổi (tạo Order + xóa CartItems + trừ Stock + Coupon.Redeem) nằm trong **một `SaveChangesAsync`** — một transaction DB (`OrderService.cs:77-82`)
- [x] Race condition (Product/Coupon `RowVersion` đổi giữa lúc đọc và lúc save) → `DbUpdateConcurrencyException` bắt lại thành 409 Conflict, không phải 500 (`OrderService.cs:84-88`, `Product.cs:23`, `Coupon.cs:24`)
- [x] Gửi email xác nhận đơn — best-effort, lỗi gửi email không làm fail checkout (`OrderService.cs:90-108`)

### Order state machine (`Order.Status`)
- [x] Enum `Pending → Paid → Shipped → Delivered`, `Cancelled` là nhánh rẽ (`src/ECommerce.Domain/Enums/OrderStatus.cs`)
- [x] `AllowedTransitions`: Pending→{Paid,Cancelled}; Paid→{Shipped,Cancelled}; Shipped→{Delivered}; Delivered/Cancelled là trạng thái cuối (`Order.cs:24-31`)
- [x] `ChangeStatus` no-op nếu status không đổi; chuyển không hợp lệ → `InvalidOrderTransitionException` (`Order.cs:33-41`)
- [x] `PUT /api/orders/{id}/status` chỉ Role `Admin`, parse string status bằng `Enum.TryParse` (case-insensitive), sai tên → 400; đơn không tồn tại → 404; transition không hợp lệ → bắt exception trả 409 (`src/ECommerce.API/Controllers/OrdersController.cs:74-83`, `OrderService.cs:148-167`)

### Cancel
- [x] `POST /api/orders/{id}/cancel` — chỉ owner của đơn (không có admin bypass ở endpoint này) (`OrdersController.cs:56-63`, `OrderService.cs:174-175`)
- [x] `Order.CanCancel()` chỉ đúng khi trạng thái hiện tại cho phép chuyển sang `Cancelled` — tức chỉ `Pending` hoặc `Paid`; sai → 409 `"Cannot cancel an order in {Status} state."` (`Order.cs:44-45`, `OrderService.cs:176-177`)
- [x] Hoàn tồn kho: cộng lại `Quantity` vào `Product.Stock` cho từng item (`OrderService.cs:179-185`)
- [x] Chỉ `OrderItem` đang `FulfillmentStatus.Pending` được chuyển sang `Cancelled`; item đã `Shipped`/`Delivered` không bị đổi (`OrderService.cs:186-187`)
- [x] `Order.ChangeStatus(Cancelled)` rồi save; concurrency conflict trên `Product.RowVersion` khi hoàn kho song song → 409 `"Đơn hàng vừa được cập nhật, vui lòng thử lại."` (`OrderService.cs:189-197`)

### Pagination
- [x] `GET /api/orders`: Admin thấy toàn bộ (`GetAllAsync`), user thường chỉ thấy đơn của mình (`GetForUserAsync`) (`OrdersController.cs:29-35`, `OrderService.cs:113-117`)
- [x] `page` clamp tối thiểu 1, `pageSize` clamp 1–100, sort `CreatedAt` giảm dần (`OrderService.cs:121-127`)
- [x] `GET /api/orders/{id}`: không tồn tại → 404; không phải chủ đơn và không phải admin → 403 (`OrderService.cs:138-146`)

### Per-seller payment split
- [x] `GET /api/orders/{id}/split` — không tồn tại → 404; không phải chủ đơn và không phải admin → 403 (`OrdersController.cs:66-72`, `OrderService.cs:208-211`)
- [x] Group `OrderItem` theo `SellerId`, sort theo `SellerId` tăng dần; tên shop lấy `User.ShopName ?? User.FullName`, fallback `"Seller #{id}"` nếu không tìm thấy user (`OrderService.cs:213-224, 246-247`)
- [x] Discount toàn đơn (`Order.DiscountAmount`) pro-rate theo tỉ lệ subtotal của từng seller: `Round(DiscountAmount * sellerSubtotal/orderSubtotal, 2)` cho mọi seller **trừ seller cuối** (`OrderService.cs:240-243`)
- [x] Seller cuối nhận phần dư làm tròn: `discountShare = Clamp(DiscountAmount - allocated, 0, sellerSubtotal)` — đảm bảo tổng các phần chia đúng bằng `DiscountAmount`, và clamp chặn giảm giá âm hoặc vượt subtotal của seller đó (`OrderService.cs:235-238`)
- [x] `NetTotal` mỗi seller = `Max(0, sellerSubtotal - discountShare)` (`OrderService.cs:250`)
- [x] `orderSubtotal == 0` → mọi `discountShare` (trừ seller cuối) = 0, tránh chia cho 0 (`OrderService.cs:240-242`)

## Ràng buộc
- Cart không giữ/khóa tồn kho — chỉ kiểm tra tại thời điểm add/update; giữa lúc thêm vào cart và lúc checkout, sản phẩm có thể hết hàng do người khác mua trước (`CartService.cs:32-33` vs `OrderService.cs:49`)
- Có 2 lớp lỗi tồn kho khác nhau ở checkout: hết hàng phát hiện trước save → 400 Validation (`OrderService.cs:60-63`); hết hàng do ghi đè đồng thời phát hiện lúc save (RowVersion) → 409 Conflict (`OrderService.cs:84-88`)
- Giá trong `CartItem` là giá sống (`Product.Price`), giá trong `OrderItem` là giá đã đóng băng lúc checkout — hai model giá khác nhau, không đồng bộ ngược (`CartItem.cs:15` vs `OrderItem.cs:18, 54-55`)
- `CanCancel()` chỉ cho phép hủy ở `Pending`/`Paid`; đơn đã `Shipped` không thể tự hủy qua endpoint này (`Order.cs:44-45`)
- `PUT /status` không có validation nghiệp vụ ngoài state machine (ví dụ set `Paid` thủ công không kiểm tra đã thanh toán thật hay chưa) — do Admin toàn quyền
- Split không có validation `DiscountAmount` khớp coupon gốc — tin dữ liệu đã lưu trên `Order`

## Quyết định
- Checkout dùng optimistic concurrency (`RowVersion` trên `Product`/`Coupon`) thay vì lock tường minh — đơn giản hơn distributed lock, đổi lại client phải tự retry khi 409.
- Snapshot `ProductName`/`UnitPrice`/`SellerId` vào `OrderItem` ngay lúc checkout để đơn hàng không đổi khi seller sửa giá/tên sản phẩm sau này — lịch sử đơn hàng bất biến.
- Không tách sub-order theo seller ở tầng dữ liệu — một `Order` vẫn là entity gốc, việc "chia" seller chỉ là view tính toán qua endpoint `/split`, không có bảng riêng.
- Rounding remainder của discount pro-rate luôn dồn cho seller cuối (theo thứ tự `SellerId` tăng) — cách đơn giản để tổng khớp chính xác `DiscountAmount`, đổi lại seller cuối luôn là người "chịu" sai số làm tròn (thường ±0.01).
- Cancel chỉ set `FulfillmentStatus.Cancelled` cho item đang `Pending`, bỏ qua item đã `Shipped` — tránh đảo trạng thái giao hàng đã xảy ra thật ngoài đời khi hủy đơn ở tầng `Order`.

## Ngoài phạm vi
- `OrderItem.Status` (`FulfillmentStatus`: Pending/Shipped/Delivered/Cancelled) — state machine riêng do seller cập nhật từng phần đơn (`OrderItem.cs:21-42`, `FulfillmentStatus.cs`), thuộc spec Fulfillment riêng
- `Payment.Status` và luồng thanh toán (`POST /api/orders/{id}/pay`, `IPaymentService`, MoMo/mock/COD) — state machine thứ 3, thuộc spec Payment riêng
- Coupon CRUD (tạo/sửa/xóa coupon) — thuộc spec Coupon/Promotion riêng, ở đây chỉ dùng `IsValidFor`/`CalculateDiscount`/`Redeem`
- Email templating/nội dung email xác nhận đơn — chỉ ghi nhận có gửi, không đặc tả nội dung
- Seller-side order view/quản lý đơn theo seller (nếu có) — không đọc trong phạm vi file được chỉ định
