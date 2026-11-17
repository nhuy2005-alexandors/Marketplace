# BÁO CÁO DỰ ÁN MARKETPLACE
## Cơ sở dữ liệu và các chức năng nghiệp vụ

> **Trạng thái tài liệu:** Bản nội dung chuẩn hóa theo source hiện tại. Trước khi nộp cần chèn ảnh chụp thật từ ứng dụng local và cập nhật kết quả nghiệm thu cuối cùng. Không sử dụng ảnh mock hoặc số liệu chưa được xác minh.

## 1. Giới thiệu

Marketplace là website thương mại điện tử nhiều người bán. Khách hàng có thể tìm kiếm sản phẩm, quản lý giỏ hàng, đặt hàng, thanh toán và theo dõi đơn hàng. Người bán quản lý sản phẩm và xử lý các mặt hàng thuộc cửa hàng của mình. Quản trị viên quản lý toàn sàn, duyệt người bán, theo dõi đơn hàng, hoàn tiền và dữ liệu khách hàng.

Mục tiêu của dự án là vận dụng SQL Server vào một ứng dụng có nghiệp vụ thực tế: thiết kế bảng và quan hệ, thực hiện CRUD, sử dụng transaction, index, constraint, stored procedure và trigger; đồng thời khai thác dữ liệu catalog và giao dịch cho chức năng AI.

## 2. Công nghệ và kiến trúc

- Backend: ASP.NET Core 9, Entity Framework Core.
- Database: Microsoft SQL Server LocalDB.
- Frontend: React, TypeScript, Vite.
- AI: hybrid semantic/lexical retrieval, recommendation ranking và RFM customer classification.
- Kiến trúc backend phân lớp: Domain, Application, Infrastructure và API.

Ứng dụng chạy local. Database development được cấu hình tường minh qua connection string ECommerceDb_Dev; API không tự động migrate hoặc seed dữ liệu khi khởi động. Migration được áp dụng có chủ đích vào đúng database development.

## 3. Các chức năng nghiệp vụ chính

| Vai trò | Chức năng |
|---|---|
| Guest | Xem trang chủ, danh mục, tìm kiếm và xem chi tiết sản phẩm |
| Customer | Đăng ký/đăng nhập, giỏ hàng, coupon, checkout, thanh toán, lịch sử đơn hàng, yêu cầu hoàn tiền, review và wishlist |
| Seller | Dashboard riêng, quản lý sản phẩm, xem đơn có sản phẩm của mình và cập nhật trạng thái fulfillment theo từng mặt hàng |
| Admin | Dashboard toàn sàn, quản lý catalog/brand/attributes, duyệt seller, quản lý đơn và xử lý yêu cầu hoàn tiền |
| AI | Shopping Assistant dựa trên catalog, gợi ý sản phẩm và phân loại khách hàng RFM |

### 3.1 Ảnh giao diện cần chèn vào bản nộp

Chụp trực tiếp từ ứng dụng local sau khi chạy đúng database và tài khoản demo. Mỗi ảnh cần có chú thích vai trò và chức năng đang minh họa.

| Hình | Nội dung cần chụp | Trạng thái |
|---|---|---|
| 1 | Trang chủ và danh sách sản phẩm | Chờ ảnh local |
| 2 | Tìm kiếm, lọc sản phẩm và trang chi tiết | Chờ ảnh local |
| 3 | Giỏ hàng và checkout | Chờ ảnh local |
| 4 | Kết quả thanh toán và chi tiết đơn hàng | Chờ ảnh local |
| 5 | Lịch sử đơn hàng và yêu cầu hoàn tiền của Customer | Chờ ảnh local |
| 6 | Seller dashboard và danh sách đơn hàng | Chờ ảnh local |
| 7 | Seller quản lý sản phẩm và cập nhật fulfillment | Chờ ảnh local |
| 8 | Admin dashboard: thống kê trạng thái đơn và sản phẩm bán chạy | Chờ ảnh local |
| 9 | Admin quản lý sản phẩm, brand và thuộc tính catalog | Chờ ảnh local |
| 10 | Admin quản lý refund và màn hình RFM | Chờ ảnh local |
| 11 | AI Shopping Assistant trả lời dựa trên sản phẩm trong catalog | Chờ ảnh local |
| 12 | Khu vực recommendation / sản phẩm tương tự | Chờ ảnh local |

Không chụp màn hình chứa API key, access token, thông tin thanh toán nhạy cảm hoặc dữ liệu cá nhân không cần thiết.

## 4. Thiết kế cơ sở dữ liệu

Database hiện có 16 bảng nghiệp vụ:

1. Users
2. Categories
3. Products
4. Brands
5. AttributeDefinitions
6. ProductAttributeValues
7. Carts
8. CartItems
9. Orders
10. OrderItems
11. Payments
12. Refunds
13. Reviews
14. WishlistItems
15. Coupons
16. RefreshTokens

Các quan hệ chính:

- Một User có thể đặt nhiều Order; một Order có nhiều OrderItem.
- Product liên kết Category, Seller và Brand; Product có thể có nhiều ProductAttributeValue.
- Cart thuộc một User và có nhiều CartItem.
- Order có Payment theo quan hệ một-một và có thể có Refund.
- OrderItem lưu snapshot tên sản phẩm, người bán và đơn giá tại thời điểm đặt hàng để bảo toàn lịch sử giao dịch.

Toàn bộ schema được quản lý bằng EF Core migrations. Có 16 migrations trong baseline trước phần bổ sung phục vụ yêu cầu Stored Procedure; migration mới 20261117130000_AddDashboardStoredProcedures chỉ tạo Stored Procedure và cập nhật trigger, không thêm bảng/cột hoặc thay đổi dữ liệu nghiệp vụ. Migration mới phải được áp dụng và xác minh trên LocalDB trước khi đánh dấu hoàn tất.

### 4.1 Khóa và ràng buộc toàn vẹn

- Primary Key xác định duy nhất mỗi bản ghi.
- Foreign Key bảo vệ quan hệ giữa User, Product, Order, OrderItem, Payment, Refund và các bảng liên quan.
- Unique indexes bảo vệ các business key như SKU, email, cart theo user, item trong cart, payment theo order/provider reference và review theo product/user.
- CHECK constraints giới hạn các giá trị như giá và tồn kho không âm, số lượng dương, rating từ 1 đến 5 và trạng thái nằm trong miền enum hợp lệ.
- RowVersion được dùng cho optimistic concurrency ở các thực thể có nguy cơ cập nhật đồng thời như Product, Coupon, Order, OrderItem, Payment và Refund.

## 5. CRUD và truy vấn có tham số

Các thao tác Create, Read, Update, Delete được thực hiện qua service và Entity Framework Core:

- Product: tạo, xem, cập nhật và xóa theo quyền Admin/Seller.
- Cart: thêm, xem, cập nhật số lượng và xóa sản phẩm trong giỏ.
- Order: checkout, xem lịch sử, hủy và theo dõi trạng thái.
- Catalog: quản lý Category, Brand và typed attributes.
- Admin: quản lý seller, refund và dữ liệu dashboard.

Các truy vấn LINQ được EF Core chuyển thành SQL có tham số. Không ghép trực tiếp dữ liệu người dùng vào chuỗi SQL. Stored Procedure cũng nhận @SellerId và @Take qua parameter thay vì nối chuỗi.

## 6. Index và tối ưu truy vấn

Index được đặt theo khóa tra cứu, quan hệ và ràng buộc nghiệp vụ. Một số ví dụ:

| Index | Mục đích |
|---|---|
| Unique Products.Sku | Tra cứu SKU và ngăn SKU trùng |
| Products.CategoryId, SellerId, BrandId | Lọc catalog theo danh mục, người bán và thương hiệu |
| Unique Carts.UserId | Mỗi khách hàng có tối đa một giỏ hàng |
| Unique CartItems(CartId, ProductId) | Không tạo dòng sản phẩm trùng trong cùng giỏ |
| Orders.UserId | Lấy đơn theo khách hàng |
| OrderItems.OrderId, ProductId, SellerId | Tra cứu dòng đơn theo đơn, sản phẩm và seller |
| Unique Payments.OrderId | Một payment record cho mỗi order |
| Unique filtered Payments(ProviderKey, TransactionId) và (ProviderKey, ProviderReference) | Chống ghi nhận trùng giao dịch/provider reference |
| Unique Reviews(ProductId, UserId) | Một khách hàng không tạo review trùng cho cùng sản phẩm |
| ProductAttributeValues(AttributeDefinitionId, ValueNumber/ValueBoolean) | Lọc sản phẩm theo thuộc tính kiểu số/boolean |

Index vừa phục vụ hiệu năng vừa thực thi các quy tắc duy nhất. Không kết luận một index cụ thể làm truy vấn nhanh hơn nếu chưa có execution plan hoặc phép đo IO/time tương ứng.

## 7. Transaction và concurrency

### 7.1 Checkout

Checkout cập nhật tồn kho, tạo Order và OrderItem, áp dụng coupon và cập nhật giỏ hàng trong cùng một lần SaveChangesAsync. Với SQL Server provider, EF Core sử dụng transaction cho một lần SaveChanges để các thay đổi cùng thành công hoặc cùng rollback.

### 7.2 Refund

Các bước cập nhật Refund, Payment, Order, OrderItem và tồn kho trong thao tác hoàn tiền được lưu cùng một transaction database. Nếu một thay đổi thất bại, transaction rollback để không để lại trạng thái một phần.

### 7.3 Payment gateway

Gọi dịch vụ thanh toán bên ngoài không thể nằm trong transaction SQL Server. Ứng dụng dùng payment attempt, callback verification và reconciliation để xử lý kết quả từ provider. Vì vậy không mô tả transaction database như thể nó bao phủ cả hệ thống thanh toán bên ngoài.

### 7.4 Optimistic concurrency

RowVersion giúp phát hiện hai request đồng thời sửa cùng một dữ liệu. Ứng dụng xử lý conflict thay vì âm thầm ghi đè cập nhật trước đó.

## 8. Stored Procedures

Migration 20261117130000_AddDashboardStoredProcedures bổ sung hai Stored Procedure phục vụ dashboard. Chúng được gọi thực tế từ DashboardService thông qua DashboardStoredProcedureQueries.

### 8.1 dbo.usp_GetOrderStatusSummary

Tham số:
- @SellerId INT = NULL: NULL lấy dữ liệu toàn sàn; có giá trị thì chỉ thống kê các đơn có OrderItem thuộc seller đó.

Kết quả: trạng thái đơn hàng và số lượng đơn tương ứng. EXISTS được sử dụng để tránh đếm trùng một đơn có nhiều OrderItem của cùng seller.

Nơi áp dụng: Admin Dashboard và Seller Dashboard, khu vực “Đơn theo trạng thái”.

### 8.2 dbo.usp_GetTopSellingProducts

Tham số:
- @SellerId INT = NULL: phạm vi toàn sàn hoặc một seller.
- @Take INT = 5: số sản phẩm trả về; giá trị được giới hạn an toàn trong procedure.

Kết quả: ProductId, ProductName, UnitsSold và Revenue.

Quy tắc dữ liệu:
- Chỉ lấy Order có trạng thái Paid, Shipped hoặc Delivered.
- Payment phải Completed.
- Loại payment demo và payment đang yêu cầu reconciliation.
- Loại OrderItem đã Cancelled.
- Có thể giới hạn theo seller.

Nơi áp dụng: Admin Dashboard và Seller Dashboard, khu vực “Top sản phẩm bán chạy”.

Hai procedure chỉ đọc dữ liệu, không thay thế transaction hoặc business logic Checkout/Payment/Refund trong application service.

## 9. Trigger

Trigger: dbo.TR_OrderItems_ProtectSnapshot.

Trigger được áp dụng trên bảng OrderItems cho ba sự kiện INSERT, UPDATE và DELETE.

| Sự kiện | Quy tắc |
|---|---|
| INSERT | Snapshot SellerId, ProductName và UnitPrice của OrderItem phải khớp Product tại thời điểm tạo |
| UPDATE | Không cho sửa OrderId, ProductId, SellerId, ProductName, UnitPrice và Quantity; trạng thái fulfillment vẫn được cập nhật |
| DELETE | Không cho xóa vật lý OrderItem để bảo toàn lịch sử giao dịch và snapshot của đơn hàng |

Khi vi phạm, trigger phát sinh lỗi SQL để hủy thao tác. Các thay đổi trạng thái giao hàng hợp lệ không bị chặn vì Status không thuộc nhóm trường snapshot bất biến.

Trigger được quản lý trong migration để có thể áp dụng lại trên database mới và rollback có kiểm soát.

## 10. Khai thác dữ liệu bằng AI

- AI Shopping Assistant: kết hợp semantic retrieval và lexical retrieval trên catalog; dữ liệu giá, tồn kho và thuộc tính phải lấy từ nguồn SQL/evidence, không để mô hình tự suy đoán.
- Recommendation: xếp hạng sản phẩm theo tín hiệu phù hợp và điều kiện eligibility của Marketplace.
- RFM: phân nhóm khách hàng theo Recency, Frequency và Monetary; dùng giao dịch hợp lệ, canonical currency USD và quyền truy cập Admin.

AI là lớp khai thác dữ liệu trên database hiện hữu, không thay thế SQL Server làm nguồn dữ liệu chuẩn.

## 11. Kiểm thử và nghiệm thu

Bản báo cáo cuối phải cập nhật kết quả từ lần chạy trên branch và database local cuối cùng:

| Hạng mục | Kết quả cuối |
|---|---|
| Backend build/unit tests | Cập nhật sau lần chạy cuối |
| Frontend tests/build | Cập nhật sau lần chạy cuối |
| SQL Server migration 17 | Cập nhật sau khi áp dụng trên disposable LocalDB |
| Hai Stored Procedures | Cập nhật sau khi gọi qua DashboardService |
| Trigger INSERT/UPDATE/DELETE | Cập nhật sau kiểm thử SQL Server |
| Customer/Seller/Admin UI | Cập nhật sau browser smoke |
| Gemini live | Cập nhật theo kết quả runtime thực tế |

Không ghi PASS cho hạng mục chỉ mới có source code mà chưa được chạy. Không ghi SQL SKIP thành PASS.

## 12. Giới hạn hiện tại

- VNPAY status query còn trường hợp trả mã 99 cần được ghi nhận đúng theo kết quả runtime.
- Hoàn tiền qua gateway bên ngoài chưa tự động hoàn tất; hệ thống quản lý refund state và reconciliation, việc chuyển tiền bên ngoài cần được xác nhận theo quy trình hiện tại.
- Gemini có thể bị quota/rate-limit; retrieval cần giữ fallback an toàn.
- Ứng dụng hiện hướng đến demo local, không tuyên bố production-ready.

## 13. Kết luận

Marketplace áp dụng SQL Server cho dữ liệu người dùng, catalog, giỏ hàng, đơn hàng, thanh toán, hoàn tiền và đánh giá. EF Core quản lý CRUD và migration; index, foreign key, unique/check constraints bảo vệ tính toàn vẹn; transaction và RowVersion bảo vệ các luồng cập nhật quan trọng. Hai Stored Procedure phục vụ dashboard và trigger bảo vệ snapshot OrderItem. Các chức năng AI khai thác catalog và giao dịch hiện hữu để hỗ trợ tìm kiếm, đề xuất và phân loại khách hàng.

Bản nộp cuối cần kèm ảnh giao diện thực tế, kết quả kiểm thử sau migration mới và minh chứng procedure/trigger được áp dụng trên SQL Server.
