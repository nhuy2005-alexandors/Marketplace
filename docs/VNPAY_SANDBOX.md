# Chạy VNPAY Sandbox trên Marketplace local

VNPAY là provider riêng, dùng chung payment/refund lifecycle. MoMo vẫn giữ trong source; cấu hình local hiện tắt MoMo. Không chạy migrations, reseed hoặc đổi database để bật VNPAY.

## Lấy thông tin Sandbox

Đường đăng ký mới được tài liệu thanh toán chính thức trỏ tới: https://pay.vnpaytest.vn/dev-registration . Đường `sandbox.vnpayment.vn/devreg` cũ hiện có thể trả404. Dùng thông tin thật của Owner và website của dự án. Owner tự hoàn thành reCAPTCHA và kích hoạt tài khoản trong email nếu được yêu cầu.

Nếu báo “Email hoặc website đã được đăng ký”, chưa thể kết luận email hay website bị trùng. Kiểm tra phản hồi và thông tin hiện có; không dùng mật khẩu tự tạo cho lần đăng ký thất bại để suy ra mật khẩu của tài khoản cũ.

## Cấu hình riêng của máy

Trong `src/ECommerce.API/appsettings.Local.json` (gitignored), chỉnh **chỉ section `Payment.Vnpay`**, giữ nguyên connection/JWT và các section khác:

```json
"Vnpay": {
  "TmnCode": "",
  "HashSecret": "",
  "CatalogCurrency": "USD",
  "CatalogToVndRate": 25637,
  "ReturnUrl": "http://localhost:5215/api/payments/vnpay/return",
  "QueryIpAddress": "127.0.0.1"
}
```

Điền `vnp_TmnCode`/Terminal Code và `vnp_HashSecret`/Secret Key từ thông tin kết nối Sandbox của VNPAY. Chúng khác mật khẩu đăng nhập merchant. Không dùng thông tin mẫu để giả bộ đã có credentials; không đưa key vào Git, chat hoặc log. Môi trường `Payment__Vnpay__...` ghi đè cấu hình file nếu có.

Tỷ giá Owner đã duyệt là **25637 VND cho1USD** cho giao dịch mới. Provider quote làm tròn wholeVND, được lưu riêng với USD canonical; `vnp_Amount` trong giao thức là VND×100. Payment cũ không bị quy đổi lại. QueryIpAddress là IP của server thực hiện query; cấu hình loopback trên máy phát triển, thay bằng IP server đúng khi có yêu cầu từ VNPAY.

Hai endpoint outbound được khóa ở Sandbox: `https://sandbox.vnpayment.vn/paymentv2/vpcpay.html` và `https://sandbox.vnpayment.vn/merchant_webapi/api/transaction`. Không dùng production credentials.

Khởi động lại API sau khi lưu cấu hình. Development runtime vẫn bắt buộc named LocalDB `ECommerceDb_Dev`; không dùng default DB gốc.

## Return, IPN và query

- Chọn **VNPAY Sandbox** trong checkout. Thiếu credentials bị chặn trước khi reserve Payment; Order đã checkout vẫn có thể được hủy theo guard hiện có để hoàn stock.
- Return `GET /api/payments/vnpay/return` chỉ xác minh payload và đưa browser về Orders. Nó **không** ghi Paid/Completed. Signed return mang orderId để nút **Kiểm tra với VNPAY** gọi query được xác thực.
- IPN `GET /api/payments/vnpay/ipn` phải được đăng ký với VNPAY bằng URL HTTPS truy cập được từ Internet. Không gửi `vnp_IpnUrl` tự chế trong payment URL.
- Query `POST /api/payments/vnpay/orders/{id}/query` yêu cầu login và quyền Owner/Admin của order. Chỉ response server-to-server có chữ ký hợp lệ, đúng merchant/reference/type và trạng thái cuối mới được áp dụng qua shared capture transition.
- Exact duplicate không xử lý lại. Capture sau failure/cancellation, amount mismatch và transaction ID collision đều giữ hold đối soát; không tự mở fulfillment.
- Refund hiện là MVP hoàn toàn bộ đơn do Admin ghi nhận sau khi đối chiếu/chuyển tiền bên ngoài. Chưa triển khai API hoàn tiền tự động VNPAY và không chuyển tiền thật khi nghiệm thu.

## HTTPS bridge tạm cho IPN

Lượt triển khai chuẩn bị `vnpay-public-bridge.mjs` bên ngoài repository trong thư mục evidence của Owner. Bridge chỉ bind loopback5316, phục vụ landing page và chuyển tiếp đúng GET IPN về localAPI5215; mọi API khác bị404. Self-test và probe HTTPS bên ngoài xác minh giới hạn này. Không công khai frontend/Admin/auth endpoints.

Cloudflared portable được kiểm tra SHA256 với asset release chính thức, không cài global hoặc sửa PATH. Quick Tunnel có URL tạm; giữ process chạy khi test. Nếu URL thay đổi, cập nhật IPN đã đăng ký. URL và cách chạy cụ thể nằm trong evidence local, không commit keys hoặc thông tin tài khoản vào hướng dẫn này. Query đã xác thực là cách kiểm tra kết quả khi IPN chưa đến; nó không giả lập capture.

Trong lượt triển khai này, tài khoản SIT tại `https://sandbox.vnpayment.vn/vnpaygw-sit-testing/user/login` có mục **Cấu hình IPN URL** theo Terminal Code; URL trước đó là `#`. Chỉ sửa đúng terminal khớp cấu hình local, lưu URL GET IPN và đọc lại sau lưu. Không lưu form TerminalEdit thiếu terminal selection như thể đó là cấu hình thực.

## Trạng thái live được xác minh

- Đăng ký mới HTTP201, Owner kích hoạt email và nhập credentials private. Browser tới cổng sandbox, hoàn thành thẻ test NCB; Order5007 trong SQL đãPaid/PaymentCompleted non-demo, capture reference có mặt. Refund1 request/hold/blocked-fulfillment/reject/release-hold đã kiểm tra với browser/API/SQL thật.
- Query outbound dùng User-Agent `Marketplace-Sandbox/1.0`; đây là header định danh ứng dụng, không thay kiểm tra HMAC. Probe .NET không có header bị403HTML; có header nhận200JSON. Sau sửa header, query thực vẫn trả mã99 không có chữ ký, nên adapter không thay DB. Chưa chứng nhận positive live query; Order5006 cònPending do tạo trước khi IPN được cấu hình. Không gửi lại/cancel tự động hoặc biến browser Return thành quyền ghiPaid.
- Chưa thực hiện external refund hoặc Complete trên gateway. Không nhập chứng từ giả. Quick Tunnel/SQL facts bịskip và query99 là các giới hạn còn lại được ghi trong báo cáo evidence; không phải production acceptance.

## Phạm vi bằng chứng

Unit/HTTP integration fixtures kiểm tra signing, amount, Return không ghi DB, IPN casing/duplicate keys, query/ownership, replay và reconciliation. Chúng không chứng nhận cổng VNPAY thật hoặc SQL Server concurrency. Live Sandbox success chỉ được ghi PASS sau khi có credentials thực, thanh toán test qua cổng và xác nhận qua IPN/query thật.

Tài liệu: https://sandbox.vnpayment.vn/apis/docs/thanh-toan-pay/pay.html và https://sandbox.vnpayment.vn/apis/docs/truy-van-hoan-tien/querydr%26refund.html .
