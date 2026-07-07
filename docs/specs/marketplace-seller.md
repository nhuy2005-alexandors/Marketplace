# Marketplace / Seller — Spec

> Spec hồi tố từ code hiện có.

## Mục tiêu
Cho phép user đăng ký làm seller, chờ Admin duyệt trước khi được bán hàng, có trang shop công khai và tự quản lý giao hàng cho từng item trong đơn của mình.

## Yêu cầu

- [x] Đăng ký seller qua `POST /api/auth/register-seller` với `Email, Password, FullName, ShopName` (`AuthController.cs:24-29`, `AuthDtos.cs:5`). `ShopName` bắt buộc, tối đa 150 ký tự (`RequestValidators.cs:37`).
- [x] User tạo ra có `Role = Seller`, `SellerStatus = Pending` (`AuthService.cs:48-56`). Email vẫn phải unique toàn hệ thống, chung namespace với customer (`AuthService.cs:44-46`).
- [x] `User.SellerStatus` nullable: `null` = không phải seller; `Pending`/`Approved` chỉ áp dụng cho `Role = Seller` (`User.cs:15-16`, `SellerStatus.cs:4-8`).
- [x] Admin xem danh sách seller, lọc theo status qua `GET /api/admin/sellers?status=` (`AdminController.cs:29-34`), chỉ lấy `Role = Seller` (`SellerAdminService.cs:29`), sort Pending trước, cũ trước (`SellerAdminService.cs:33-35`).
- [x] Admin duyệt seller qua `POST /api/admin/sellers/{id}/approve` (`AdminController.cs:36-43` → `SellerAdminService.ApproveAsync`, `SellerAdminService.cs:42-71`): 404 nếu không tồn tại/không phải seller, 409 nếu đã Approved, chuyển sang `Approved` và gửi email thông báo best-effort (lỗi gửi email bị nuốt, không fail request) (`SellerAdminService.cs:55-66`).
- [x] Tạo sản phẩm bị chặn nếu seller chưa Approved: `ProductService.CreateAsync` check `Role == Seller && SellerStatus != Approved` → 403 Forbidden; Admin (`SellerStatus == null`) không bị chặn (`ProductService.cs:84-92`).
- [x] `POST/PUT/DELETE /api/products` yêu cầu role `Admin,Seller` (`ProductsController.cs:34,43,53`); update/delete còn check ownership `product.SellerId != actorId` (non-admin) → 403 (`ProductService.cs:123-124`, `145-146`).
- [x] Trang shop công khai `GET /api/sellers/{id}/shop` (`SellersController.cs:14-18` → `ProductService.GetSellerShopAsync`) chỉ trả seller có `Role == Seller && SellerStatus == Approved`; seller Pending trả 404, tức bị ẩn khỏi public (`ProductService.cs:160-171`). Danh sách sản phẩm của shop dùng lại `GET /api/products?sellerId=` (`SellersController.cs:13`, `ProductService.cs:33-34`).
- [x] `FulfillmentStatus` là state machine riêng trên từng `OrderItem`, độc lập với `Order.Status`: `Pending → {Shipped, Cancelled}`, `Shipped → {Delivered}`, `Delivered`/`Cancelled` là trạng thái cuối (`OrderItem.cs:25-31`). Set lại chính trạng thái hiện tại là no-op; transition không hợp lệ throw `InvalidOrderTransitionException` (`OrderItem.cs:34-41`).
- [x] Seller cập nhật fulfillment qua `PUT /api/seller/orders/items/{itemId}/status`, class-level `[Authorize(Roles = "Seller")]` (`SellerController.cs:10,39-47`). Service check ownership `item.SellerId != sellerId` → 403 Forbidden (`SellerOrderService.cs:78-79`); item không tồn tại → 404 (`SellerOrderService.cs:76-77`); status string không parse được → 400 Validation (`SellerOrderService.cs:72-73`); transition sai → 409 Conflict (`SellerOrderService.cs:81-88`).
- [x] Seller dashboard `GET /api/seller/dashboard` (`SellerController.cs:23-28`) và order list `GET /api/seller/orders` (`SellerController.cs:30-36`) scoped theo `sellerId` từ JWT (`UserId`): revenue/orders/products/status-breakdown/top-products đều filter theo `SellerId` (`DashboardService.cs:14,30-52,56-69`); order list chỉ trả item của seller đó, ẩn item của seller khác trong cùng đơn (`SellerOrderService.cs:21-22,36-38`).
- [x] Giảm giá coupon toàn đơn được chia tỉ lệ cho phần seller: `sellerDiscount = order.DiscountAmount * (sellerSubtotal / orderSubtotal)`, round 2 chữ số (`SellerOrderService.cs:41-45`, công thức lặp lại ở `DashboardService.cs:39`).

## Ràng buộc
- Duyệt seller là one-way, không có endpoint reject/revoke — chỉ `Pending → Approved` (`SellerAdminService.cs`, `AdminController.cs`: không có route khác ngoài `approve`).
- Gửi email duyệt là best-effort, không transactional với việc đổi status — nếu email fail, status vẫn đã Approved (`SellerAdminService.cs:51-66`).
- Ownership check cho product update/delete và fulfillment update đều so `SellerId` với `UserId` trong JWT, không có khái niệm nhân viên/nhiều user quản 1 shop.

## Quyết định
- Không tạo trạng thái `Rejected` riêng — hồi tố đúng những gì code có (`Pending`/`Approved` only), vì spec là tài liệu mô tả code hiện có, không phải đề xuất mở rộng.
- Public shop endpoint dùng field `SellerStatus == Approved` trực tiếp trong query (không cache/flag riêng) — đơn giản, đúng theo `ProductService.cs:164-165`.

## Ngoài phạm vi
- Seller reject/revoke, seller tự nghỉ bán (deactivate).
- Nhiều user quản lý chung 1 shop (multi-user seller account).
- Seller-side coupon riêng theo shop (coupon hiện là toàn hệ thống, chỉ pro-rate lúc tính doanh thu).
- Notification real-time (chỉ có email best-effort lúc approve).
