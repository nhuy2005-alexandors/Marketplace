# Minh chứng nghiệm thu Marketplace

Chụp ngày 18/11/2026 theo giờ Việt Nam. Baseline `502d5c0514dfdcf163fe7ce22b85ed0e97c847f9`, branch `codex/marketplace-course-db-completion-20261117`. PR #8 được xác nhận đang Draft.

Ảnh SQL là ảnh chụp phần trình bày câu lệnh và đầu ra thật từ `sqlcmd` hoặc terminal kiểm thử EF Core SQL Server. Tệp `.txt` cùng tên lưu câu lệnh và đầu ra. Các truy vấn đọc dùng `(localdb)\MarketplaceRefundDev20261113 / ECommerceDb_Dev`. Các ca ghi TR-02–05 và TX-01–02 chỉ dùng `(localdb)\MarketplaceSafetyTestEvidence20261118 / ECommerceDb_Dev`. Database, MDF/LDF và instance disposable đã được dọn. Hình gợi ý được chụp trên ứng dụng tại localhost.

| Ảnh | Mục báo cáo | Thao tác và kết quả |
|---|---|---|
| [DB-01_tables.png](DB-01_tables.png) | 2.3 | `sys.tables`: 16 bảng, bỏ bảng lịch sử EF. |
| [DB-02_migrations.png](DB-02_migrations.png) | 2.3, 4.1 | `__EFMigrationsHistory`: 17 dòng, cuối là `20261117130000_AddDashboardStoredProcedures`. |
| [SP-01_procedures.png](SP-01_procedures.png) | 3.4 | `sys.procedures`: hai thủ tục trong schema `dbo`. |
| [SP-02_order_summary.png](SP-02_order_summary.png) | 3.4 | `EXEC dbo.usp_GetOrderStatusSummary @SellerId=NULL`: 5 nhóm, tổng 5.011 đơn. |
| [SP-03_top_selling.png](SP-03_top_selling.png) | 3.4 | `EXEC dbo.usp_GetTopSellingProducts @SellerId=NULL, @Take=5`: 5 sản phẩm, kèm số lượng và doanh thu. Tên lấy từ snapshot OrderItem. |
| [TR-01_trigger_definition.png](TR-01_trigger_definition.png) | 3.5 | `sys.trigger_events` và `OBJECT_DEFINITION`: trigger đang bật và định nghĩa đủ 3 sự kiện. |
| [TR-02_insert_rejected.png](TR-02_insert_rejected.png) | 3.5 | INSERT sai tên snapshot, SQL Server trả 51006. |
| [TR-03_update_rejected.png](TR-03_update_rejected.png) | 3.5 | UPDATE tên snapshot, SQL Server trả 51007. |
| [TR-04_delete_rejected.png](TR-04_delete_rejected.png) | 3.5 | DELETE OrderItem, SQL Server trả 51008. |
| [TR-05_status_update.png](TR-05_status_update.png) | 3.5 | UPDATE Status từ 0 sang 1, đọc lại được 1 và còn đúng 1 dòng OrderItem. |
| [IX-01_indexes.png](IX-01_indexes.png) | 3.6 | `sys.indexes`: 18 chỉ mục trên CartItems, OrderItems, Payments, Products. |
| [TX-01_checkout_rollback.png](TX-01_checkout_rollback.png) | 3.3 | `OrderService.CheckoutAsync`; trigger của fixture ném 51991 sau khi thấy Order đã được ghi trong transaction. Trước/sau đều 0 order, 0 item, 3 dòng giỏ, tồn kho 1. |
| [TX-02_rowversion_conflict.png](TX-02_rowversion_conflict.png) | 3.3 | Hai DbContext cùng đọc RowVersion của sản phẩm còn một đơn vị. Quan sát `DbUpdateConcurrencyException`, đúng một checkout commit, tồn kho về 0. |
| [AI-01_recommendations.png](AI-01_recommendations.png) | 3.8 | Trang chủ hiển thị “Sản phẩm phổ biến” từ API recommendation HTTP 200. |

## Tái sử dụng và giới hạn

- Giữ 12 ảnh B.1–B.12 ở [thư mục course-20261117](../course-20261117): trang chủ, chi tiết, giỏ, checkout, lịch sử đơn, dashboard và đơn hàng Seller, dashboard Admin, catalog, refund, RFM, chatbot. B.8 ghép được với SP-02 và SP-03.
- B.9 cho thấy màn hình quản lý catalog. Bộ hiện tại chưa có ảnh thao tác lưu thêm/sửa sản phẩm hoặc ca từ chối seller. B.6–B.7 là phạm vi Seller rỗng. B.10 là giao diện quản lý refund, chưa có ảnh mới cho chuyển trạng thái.
- VNPAY sandbox và kết quả đối soát chưa tái hiện trong lượt này. Mã 99 là kết quả đã ghi nhận trước đây. B.12 minh họa fallback chatbot. API báo hybrid sẵn sàng khi kiểm tra hôm nay, chưa xác nhận Gemini sinh câu trả lời trực tiếp.
- Harness, kịch bản SQL với guard đích, đầu ra thi hành và kết quả cleanup lưu riêng trong thư mục bằng chứng local. Phụ lục B trên Google Docs chỉ được đọc để kiểm kê.
