# Product Catalog — Spec

> Spec hồi tố từ code hiện có.

## Mục tiêu
Cho phép buyer tìm/lọc/sắp xếp sản phẩm có phân trang; Seller/Admin quản lý CRUD sản phẩm và Admin quản lý category, kèm upload ảnh an toàn và chống oversell bằng optimistic concurrency.

## Yêu cầu

### Search / filter / sort / paging
- [x] Lọc theo từ khóa trên `Name` hoặc `Description` (case-sensitive `Contains`, chưa trim khác biệt hoa/thường ở DB) (`src/ECommerce.Application/Services/ProductService.cs:25-30`)
- [x] Lọc theo `CategoryId`, `SellerId`, `MinPrice`, `MaxPrice` (`ProductService.cs:31-38`)
- [x] Sắp xếp theo `price`, `name`, `rating` (trung bình `Reviews.Rating`, 0 nếu chưa có review), `stock`; mặc định `CreatedAt`; hướng `asc`/`desc` qua flag `Desc` (`ProductService.cs:40-49`)
- [x] Phân trang: `Page` clamp về tối thiểu 1, `PageSize` clamp 1–100, mặc định `PageSize=12` (`ProductService.cs:52-53`, `src/ECommerce.Application/DTOs/Catalog/CatalogDtos.cs:41-50`)
- [x] Trả `PagedResult<ProductDto>` gồm `Items`, `Page`, `PageSize`, `TotalCount`, `TotalPages` tính từ `Ceiling(TotalCount/PageSize)` (`src/ECommerce.Application/Common/PagedResult.cs:5-9`)
- [x] Endpoint public, không cần auth: `GET /products` (`src/ECommerce.API/Controllers/ProductsController.cs:23-26`)

### CRUD Product
- [x] `GET /products/{id}` public, 404 nếu không tồn tại (`ProductsController.cs:28-32`, `ProductService.cs:69-80`)
- [x] `POST /products` chỉ Role `Admin,Seller`; Seller chưa được duyệt (`SellerStatus != Approved`) bị chặn tạo sản phẩm, Admin bỏ qua check này (`ProductsController.cs:34-41`, `ProductService.cs:84-92`)
- [x] Tạo product validate `CategoryId` phải tồn tại, trả `ErrorType.Validation` nếu không (`ProductService.cs:94-95`)
- [x] `PUT /products/{id}` owner-scoped: Seller chỉ sửa được sản phẩm của chính mình (`SellerId == actorId`), Admin sửa được mọi sản phẩm (`ProductService.cs:123-124`)
- [x] `DELETE /products/{id}` owner-scoped tương tự Update; nếu sản phẩm đã nằm trong đơn hàng (FK Restrict) thì `DbUpdateException` bị bắt và trả `409 Conflict` thay vì lỗi 500 (`ProductService.cs:145-157`)

### CRUD Category
- [x] `GET /categories` public, sắp xếp theo `Name` (`src/ECommerce.API/Controllers/CategoriesController.cs:14-17`, `src/ECommerce.Application/Services/CategoryService.cs:15-19`)
- [x] `POST /categories` Role `Admin,Seller`; chặn trùng tên (case-sensitive `==`), trả `409 Conflict` (`CategoriesController.cs:19-27`, `CategoryService.cs:21-30`)
- [x] `DELETE /categories/{id}` chỉ `Admin`; chặn xóa nếu còn Product tham chiếu `CategoryId`, trả `409 Conflict` (`CategoriesController.cs:29-37`, `CategoryService.cs:32-42`)

### Upload ảnh
- [x] `POST /products/upload-image` Role `Admin,Seller`; giới hạn request 5MB qua `[RequestSizeLimit(5_000_000)]` (`ProductsController.cs:64-71`)
- [x] Đọc toàn bộ file vào `MemoryStream`, reject nếu rỗng hoặc > `MaxBytes = 5_000_000` (kiểm tra thứ hai, độc lập với `RequestSizeLimit`) (`src/ECommerce.Infrastructure/Storage/LocalFileStorage.cs:8, 24-29`)
- [x] Xác định định dạng ảnh bằng magic-byte sniffing (không tin phần mở rộng file): JPEG (`FF D8 FF`), PNG, GIF87a/89a, WEBP (RIFF...WEBP); từ chối nếu không khớp (`LocalFileStorage.cs:45-59`)
- [x] Lưu file với tên `GUID + extension đã detect`, trả về URL public `{PublicPath}/{name}` (`LocalFileStorage.cs:35-41`)

### Optimistic concurrency (anti-oversell)
- [x] `Product.RowVersion` là concurrency token do EF quản lý (`IsRowVersion()`) (`src/ECommerce.Domain/Entities/Product.cs:23`, `src/ECommerce.Infrastructure/Persistence/Configurations/CatalogConfigurations.cs:49`)
- [x] `Product.DecreaseStock()` throw `DomainException` nếu số lượng vượt tồn kho hiện tại (check ở tầng domain, không phải RowVersion) (`Product.cs:25-32`)
- [x] Khi checkout/cancel đơn hàng ghi đè tồn kho đồng thời, `DbUpdateConcurrencyException` (do RowVersion đổi giữa lúc đọc và lúc save) được bắt và trả `409 Conflict` cho client thay vì lỗi 500 (`src/ECommerce.Application/Services/OrderService.cs:84, 194-197`)

## Ràng buộc
- `PageSize` bị clamp cứng 1–100 dù client truyền gì (`ProductService.cs:53`)
- Upload ảnh giới hạn 5MB, chỉ hỗ trợ jpg/png/gif/webp qua magic byte, không hỗ trợ SVG hay các định dạng khác (`LocalFileStorage.cs:8, 45-59`)
- Update/Delete Product **không** nhận/kiểm tra `RowVersion` từ client — concurrency token chỉ có tác dụng ở nhánh checkout/cancel order (`ProductService.cs:114-158` không tham chiếu RowVersion)
- Category chỉ có thể xóa khi không còn Product nào tham chiếu; không có cơ chế reassign/cascade (`CategoryService.cs:37-38`)
- Search theo keyword dùng `string.Contains` trên SQL Server (thường collation case-insensitive theo default DB, nhưng không có `.ToLower()` tường minh trong code) (`ProductService.cs:28-29`)

## Quyết định
- Seller-owner-scoping cho Update/Delete kiểm tra bằng so sánh `SellerId == actorId` truyền qua tham số `actorId, isAdmin` từ controller (`UserId`, `IsAdmin` lấy từ JWT claims ở `ApiControllerBase`), không dùng attribute-based policy riêng.
- Kiểm tra seller-approval chỉ đặt ở `CreateAsync`, không lặp lại ở `UpdateAsync`/`DeleteAsync` — seller đã có sản phẩm coi như đã qua duyệt lúc tạo.
- Xóa Product dựa vào FK Restrict + bắt `DbUpdateException` thay vì query trước `OrderItems` — đơn giản hơn nhưng phải bắt exception generic của EF.
- Ảnh dùng magic-byte sniffing thay vì tin `Content-Type` header (spoofable) hoặc extension.

## Ngoài phạm vi
- Review/rating CRUD (endpoint `GET/POST /products/{id}/reviews` tồn tại trong `ProductsController.cs:80-94` nhưng thuộc feature Reviews riêng, không phải Catalog)
- Seller shop info (`GetSellerShopAsync`, `ProductService.cs:160-171`) — thuộc feature Seller Profile
- Xử lý tồn kho lúc checkout (`OrderService`) — chỉ dẫn chiếu RowVersion ở đây, spec chi tiết thuộc feature Order/Checkout
- Soft-delete / ẩn sản phẩm khi không xóa được do FK Restrict (hiện chỉ trả lỗi, chưa có API "ẩn")
- Full-text search / search engine ngoài (Elasticsearch, v.v.) — hiện chỉ dùng SQL `LIKE` qua `Contains`
