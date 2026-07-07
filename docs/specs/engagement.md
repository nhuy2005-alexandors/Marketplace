# Engagement (Reviews, Wishlist, Dashboard) — Spec

> Spec hồi tố từ code hiện có.

## Mục tiêu
Cho phép khách hàng đánh giá sản phẩm đã mua và lưu sản phẩm quan tâm vào wishlist; cho phép Admin/Seller xem báo cáo doanh thu, đơn hàng, sản phẩm bán chạy trên dashboard riêng theo phạm vi quyền.

## Yêu cầu

### Review
- [x] Rating bắt buộc 1-5, ngoài khoảng bị từ chối (`src/ECommerce.Application/Services/ReviewService.cs:28-29`).
- [x] Chỉ review được sản phẩm tồn tại (`ReviewService.cs:30-31`).
- [x] Verified-purchase: user phải có ít nhất 1 Order (không bị Cancelled) chứa ProductId mới được review, ngược lại trả `Forbidden` (`ReviewService.cs:33-37`).
- [x] Một user chỉ review một sản phẩm một lần — trùng ProductId+UserId trả `Conflict` (`ReviewService.cs:39-40`).
- [x] Comment được `Trim()` trước khi lưu, có thể null (`ReviewService.cs:47`).
- [x] Danh sách review theo sản phẩm sắp xếp mới nhất trước, kèm thông tin User (`ReviewService.cs:16-24`).
- [x] `AverageRating`/`ReviewCount` của sản phẩm tính động từ `Product.Reviews` (không lưu cột riêng), dùng `Math.Round(avg, 2)` (`src/ECommerce.Application/Services/MappingExtensions.cs:13-18`).
- [x] Sort sản phẩm theo "rating" dùng cùng công thức trung bình động, sản phẩm chưa có review coi như rating 0 (`src/ECommerce.Application/Services/ProductService.cs:44-46`).
- [x] Endpoint: `GET /api/products/{id}/reviews` (public), `POST /api/products/{id}/reviews` (`[Authorize]`) (`src/ECommerce.API/Controllers/ProductsController.cs:80-94`).

### Wishlist
- [x] Add: nếu sản phẩm không tồn tại → `NotFound`; nếu đã có trong wishlist của user thì trả `Ok()` im lặng (idempotent, không tạo trùng) (`src/ECommerce.Application/Services/WishlistService.cs:26-35`).
- [x] Remove: item không tồn tại (theo UserId+ProductId) → `NotFound` (`WishlistService.cs:37-46`).
- [x] Get: trả về danh sách kèm tên/giá/ảnh sản phẩm, mới thêm trước (`WishlistService.cs:15-24`).
- [x] Toàn bộ endpoint yêu cầu `[Authorize]`, dùng `UserId` từ token (`src/ECommerce.API/Controllers/WishlistController.cs:8-34`).

### Dashboard
- [x] Trạng thái đơn tính vào doanh thu/thống kê: `Paid`, `Shipped`, `Delivered` (`src/ECommerce.Application/Services/DashboardService.cs:16`) — không dựa trực tiếp vào `Payment.Completed` mà dựa vào `OrderStatus` của Order (đơn được chuyển sang Paid khi thanh toán xong).
- [x] Doanh thu hệ thống (Admin, `sellerId == null`) = tổng `Order.Total` của các đơn đã thanh toán (`DashboardService.cs:30-33`).
- [x] Doanh thu Seller = tổng subtotal các OrderItem của seller trong đơn, trừ phần discount coupon phân bổ theo tỉ lệ `sellerSub / orderSub` (`DashboardService.cs:34-41`).
- [x] `TotalOrders`: Admin = tổng số Order; Seller = số Order distinct có chứa item của seller (`DashboardService.cs:45-48`).
- [x] `TotalProducts`: Admin = tổng Product; Seller = Product của riêng seller (`DashboardService.cs:50-52`).
- [x] `TotalCustomers` = số User có Role = Customer, không phân biệt Admin/Seller (dùng chung, không lọc theo seller) (`DashboardService.cs:54`).
- [x] Đơn theo trạng thái (`ByStatus`): Admin = tất cả Order; Seller = chỉ Order có item của seller (`DashboardService.cs:56-63`).
- [x] Top 5 sản phẩm bán chạy tính từ item của đơn đã thanh toán, lọc theo seller nếu có, sắp theo UnitsSold giảm dần (`DashboardService.cs:65-80`).
- [x] Endpoint Admin: `GET /api/admin/dashboard`, role `Admin`, gọi `GetAsync(null)` (`src/ECommerce.API/Controllers/AdminController.cs:8,21-26`).
- [x] Endpoint Seller: `GET /api/seller/dashboard`, role `Seller`, gọi `GetAsync(UserId)` — scope theo chính seller đăng nhập (`src/ECommerce.API/Controllers/SellerController.cs:10,23-28`).

## Ràng buộc
- Review: có DB unique index trên `(ProductId, UserId)` (`OrderConfigurations.cs:64`) — chặn trùng ở tầng DB, ngoài check `AnyAsync` ở service (`ReviewService.cs:39-40`).
- Wishlist: có DB unique index trên `(UserId, ProductId)` (`OrderConfigurations.cs:77`) — race giữa check và insert bị DB chặn, service trả `Ok()` idempotent cho trùng.
- Dashboard không hỗ trợ lọc theo khoảng thời gian — luôn tính trên toàn bộ lịch sử đơn hàng (`DashboardService.cs:19-22`).

## Quyết định
- AverageRating/ReviewCount tính động (không denormalize) — chấp nhận N+1-ish tính toán trong LINQ vì dataset review nhỏ, đổi lại không cần đồng bộ lại cột khi review bị sửa/xoá (hiện chưa có API sửa/xoá review).
- Doanh thu Seller trừ coupon theo tỉ lệ subtotal — công bằng khi 1 đơn có nhiều seller dùng chung 1 coupon toàn đơn.
- Dùng chung 1 `IDashboardService.GetAsync(int? sellerId)` cho cả Admin và Seller thay vì 2 service riêng — tránh trùng logic tính doanh thu/top-product.

## Ngoài phạm vi
- Sửa/xoá review.
- Seller/Admin phản hồi (reply) review.
- Lọc/soft-delete review vi phạm nội dung (không có kiểm duyệt).
- Dashboard theo khoảng thời gian (ngày/tháng/năm) hoặc export báo cáo.
- Thông báo khi sản phẩm trong wishlist giảm giá/hết hàng.
