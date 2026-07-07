# 2026-07-05 — Marketplace UI revamp + MoMo cổng thật + chuẩn hóa docs

As-built: đã làm gì, luồng end-to-end, file đổi, quyết định lúc làm, cách verify.

## Mục tiêu
Nâng cấp giao diện sang phong cách marketplace hiện đại (tham chiếu emox/HOHO), thay thanh toán demo bằng **cổng thật MoMo**, và chuẩn hóa lại bộ tài liệu phân tích/thiết kế.

## Đã build

### 1. UI marketplace (frontend)
- **Palette** đổi indigo → **xanh dương** (`brand` scale trong `tailwind.config.js`), 1 chỗ đổi lan toàn app.
- **Header 2 tầng** (`Navbar.tsx`): tầng 1 logo + search bar tròn to + location/theme/cart/đăng nhập; tầng 2 nav danh mục ngang (fetch categories) hoặc role links (Admin/Seller). Search + category → điều hướng `/products?search=` / `?categoryId=`.
- **HomePage mới** (`pages/HomePage.tsx`): hero 2-banner (data thật, sort giá desc), perks strip, voucher strip (coupon thật), category circles, flash sale (countdown cuối ngày + badge -%), top-rated (`sortBy=rating`), sắp hết hàng (`stock ≤ 15`), featured grid.
- **Components** tách trong `components/home/`: `HeroBanners`, `VoucherStrip`, `FlashSale`, `ProductSection` (+ `LowStockSection`).
- **ProductCard** kiểu marketplace: card bo `rounded-3xl`, nút cart tròn, price đậm, `<Stars>` (sao đầy/nửa/rỗng + a11y).
- **Footer** (`components/Footer.tsx`): perks strip + 3 cột link **trỏ route thật** (Mua sắm / Tài khoản / Danh mục từ API), payment badge.
- **Icon**: thay toàn bộ emoji → `lucide-react` (fix `no-emoji-icons`), có `aria-label`/`aria-hidden`.
- **Layout rộng**: `max-w-6xl` → `max-w-7xl`, grid browse 2→3→4→5 cột responsive, `pageSize` 8→10.
- **ProductListPage** đọc URL params (`search`, `categoryId`, `sortBy`, `desc`) qua `useSearchParams` → header/footer/homepage lọc đúng; category chip pill; sort thêm "Đánh giá cao nhất".

### 2. Thanh toán MoMo (cổng thật) — thay Stripe/VNPay
- **`MoMoProvider`** (`Infrastructure/Payments/`): MoMo AIO v2 sandbox — POST `/v2/gateway/api/create` ký HMAC-SHA256 → nhận `payUrl` redirect; verify chữ ký khi callback. `requestType = payWithMethod` (cổng hiện QR ví + thẻ ATM + thẻ tín dụng). Quy đổi USD → VND (`UsdToVndRate = 25000`).
- **Endpoints** (`PaymentsController`): `GET /api/payments/momo/callback` (redirect) + `POST /api/payments/momo/ipn` (server-to-server JSON).
- **Enum** `PaymentMethod.EWallet` (momo → EWallet). Validator whitelist: `mock | cod | momo`.
- **Gỡ sạch** Stripe + VNPay: provider, options, DI, callback, config, NuGet `Stripe.net`, comment.
- **Config**: `appsettings.json` dùng test credentials sandbox công khai của MoMo (chạy cổng thật ngay). `AllowDemo = false`.
- Frontend `CheckoutPage`: option "Ví MoMo", redirect thật (`window.location.href = payUrl`).

### 3. Backend hỗ trợ
- `GET /api/coupons/active` (public) — cho VoucherStrip ở homepage.
- `ProductService` thêm sort `rating` (avg reviews) + `stock`.

### 4. Chuẩn hóa docs
- Gom 6 sơ đồ vào `docs/diagrams/` (git mv): use-cases, sequence-diagrams, activity-diagrams, state-machine, class-erd, architecture-diagrams.
- Thêm `architecture-diagrams.md` (Component + Deployment) và `phan-tich-bai-toan.md` (phân tích bài toán).
- Sync toàn bộ docs VNPay/Stripe → MoMo (SRS, use-cases, sequence, state-machine, BAOCAO, README).
- Sửa `class-erd.md`: bổ sung entity `RefreshToken` + enum/field `SellerStatus` (thiếu từ batch trước).

## Luồng thanh toán MoMo end-to-end
1. Customer chọn "Ví MoMo" ở `/checkout` → `POST /orders` tạo đơn Pending → `POST /orders/{id}/pay {method:"momo"}`.
2. `PaymentService.InitiateAsync` → `MoMoProvider.CreatePaymentAsync` gọi API MoMo (ký HMAC-SHA256) → trả `payUrl`, Payment giữ Pending.
3. FE `window.location.href = payUrl` → khách quét QR / nhập thẻ ATM test tại cổng MoMo.
4. MoMo redirect về `GET /api/payments/momo/callback` → `ConfirmAsync` → `VerifyAsync` verify chữ ký → `Finalize`: Payment Completed, Order → Paid → redirect FE `/orders?payment=success`.

## Quyết định lúc làm
- **Giữ navbar ngang** (không sidebar) — user chốt; đỡ đại tu layout.
- **MoMo thay vì VNPay/Stripe** — user chốt; MoMo sandbox có test credentials công khai nên chạy cổng thật ngay không cần đăng ký merchant.
- **`payWithMethod`** thay `captureWallet` — để cổng hiện thêm thẻ ATM (test bằng thẻ `9704 0000 0000 0018`), không chỉ QR.
- **Bỏ trang cổng giả** (`PaymentGatewayPage`) đã làm ở bước demo — vì MoMo redirect thật rồi.
- **Hero/flash "giá gốc"** là giá tổng hợp (×1.4) — app chưa có field giá gốc; đánh dấu là chỗ mở rộng.

## Cách verify
- `dotnet build ECommerce.sln`: 0 error/warning.
- Smoke test API: login `user@shop.com`/`User@123` → add cart → checkout → `pay {momo}` trả `requiresRedirect=true` + `payUrl` `test-payment.momo.vn`. `pay {vnpay}` → 400 (đã gỡ).
- `npm run build` (client): sạch.
- Thanh toán thật: chọn Ví MoMo → cổng MoMo → thẻ ATM test `9704 0000 0000 0018` (NGUYEN VAN A, 03/07, OTP bất kỳ) → order Paid.

## Chỗ cần chú ý
- **IPN không tới localhost**: MoMo server không gọi được `localhost:5215/ipn`. Trên dev, đơn chốt qua **redirect callback** (browser khách gọi localhost). Deploy public / ngrok thì IPN mới chạy.
- **App MoMo thật ≠ sandbox**: quét QR sandbox bằng app MoMo production → lỗi 1005. Phải dùng thẻ ATM test hoặc MoMo Test App.
- MoMo test credentials trong `appsettings.json` là **công khai** (mọi sample MoMo dùng chung), không phải secret thật.
