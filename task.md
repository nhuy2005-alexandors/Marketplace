# Current checkpoint — Owner local audit, 2026-11-15

## Current follow-up — 2026-11-16 VNPAY query investigation

- VERIFIED: existing 26-file source hash pin matched before this checkpoint update; no application/config/schema changes. Fresh existing-assembly VNPAY regression: 25PASS/0FAIL/0SKIP. No rebuild or full regression repeated in this diagnostic follow-up.
- VERIFIED: read-only queries for Pending5006 and Paid5007 both return HTTP200 with the same unsigned99 body. Numeric13 RequestId, numeric JSON timestamps, actual numeric TransactionNo and application/json without charset do not resolve it. An intentionally invalid-checksum query returns the identical body too; none establishes authenticated payment status. No persistent SQL write or new payment/refund.
- VERIFIED: SIT login works, but its linked /order/search returns a Whitelabel Forbidden403. Old merchantv2 notice concerns Link thanh toan migration only; it does not prove QueryDr moved. Merchant View delegates to VNPAY ID phone login; no phone/OTP flow submitted. UNKNOWN: whether QueryDr99 is a merchant entitlement, backend fault or another unisolated provider issue. Do not present the refund-only SDK issue as proof for QueryDr.
- Runtime from the prior turn had stopped. Restored actual API5215/frontend5173/limited bridge5316 with explicit isolated Development connection; Program.cs has no startup migration/seeder. API categories/products and browser catalog render PASS. Sessions: API63628, frontend39635, bridge8378, tunnel2065. Keep these services running.
- New temporary HTTPS origin: https://struct-demands-cole-bold.trycloudflare.com . Exact GET IPN is /api/payments/vnpay/ipn. Correct SIT terminal updated and persisted URL read back; public landing200, Admin/products404, invalid-IPN97. Previous sun-machine URL is SUPERSEDED. Only landing and IPN are exposed.
- SQL readback unchanged: Orders5007, Payments4706, OrderItems12507, Refunds1, migrations16, Product499stock37;5006Pending,5007Paid/non-demo/no-reconciliation. Keep5006 unmodified until authenticated provider confirmation. Evidence: C:/Users/HUY/Documents/Marketplace-Local-Completion/20261115/owner/vnpay-query-20261116/ . No commit/push/PR/merge; original DB/worktree and backups not targeted.
- Diagnostic follow-up COMPLETE, live QueryDr UNRESOLVED: published @nodepay/vnpay0.1.0 (NPM integrity verified) also uses the same endpoint and string timestamps; no demonstrated source defect identified. REPORT.md and VNPAY_SUPPORT_REQUEST_UNSENT.md prepared with requestId/time for provider tracing; not sent. Browser sessions closed and temporary auth-vault profile deleted. API/frontend/IPN tunnel remain running. Prior whole-suite results remain historical; fresh scoped25PASS was actually run.

## Active follow-up — VNPAY Sandbox (implementation verified; live query limitation)

- Owner authorized independent VNPAY provider and account registration, preserving MoMo/lifecycle and current work. Branch/HEAD below unchanged; implementation remains uncommitted and preserves pre-existing MoMo HTTP fix/tests.
- Implemented sandbox URL HMACSHA512, USD25637→wholeVND→wire×100, immutable alphanumeric reference with UTC timestamp, read-only signed Return, GET IPN with exact RspCode/Message casing, owner/Admin authenticated query, pending/final distinction, duplicate/late/failed/reused-ID reconciliation holds, real socket IP, request validator and checkout/query UI. No schema/migration/new DB/seed.
- Latest checks after User-Agent correction: build0warnings/errors,188PASS/3disposable-onlySQL SKIP. Frontend8PASS,build/lintPASS. User-Agent regression RED4fail/1pass→VNPAY25PASS. Root read actual diff/logs; independent read-only review/correction rereads found no remaining concrete defect in scoped code. No SQL locking/concurrency claim from fixture tests.
- Registered successfully through new official https://pay.vnpaytest.vn/dev-registration (HTTP201), after Owner completed reCAPTCHA. Old devreg404 and GitHub website409 resolved by using actual project HTTPS landing. Owner activated email and entered real sandbox TmnCode/HashSecret in gitignored Local.json; presence/format checked without printing values. Private registration credentials: LOCALAPPDATA/Marketplace/VnpaySandbox/registration-credentials.json; never copy into reports/source.
- Live synthetic Order5005 was created during validator/credential smoke and cancelled via browser; no Payment5005-by-order, stock restored39. Order5006 completed official NCB test-card flow and returned signed ResponseCode00/TransactionStatus00 with positive provider transaction; Return correctly left DB Pending. Query remains unconfirmed. DO NOT cancel/recreate5006 or fake IPN/capture while authoritative confirmation is pending.
- Correct SIT terminal IPN was initially#, then saved to the verified public HTTPSbridge. SeparateOrder5007 completed sandbox card flow after IPN setup; SQLPaymentCompleted/OrderPaid at2026-11-15T18:10:15Z, IsDemo=false, RequiresReconciliation=false, captureIDpresent. Returnnevermutates and everyqueryremainedunauthenticated, so IPN is the source-correlated explanation; no raw inboundpayloadtrace preserved (bridge receiptlogging added after capture).
- CustomerbrowserRefund1 forOrder5007→Pending/RefundPendingtrue; realAPI Adminship/cancel/duplicaterefund409. AdminbrowserReject→Rejected, holdfalse, OrderPaid/PaymentCompleted unchanged; stock37 unchanged. No externalrefund/Complete, fakecapture or restock. Query99 remains known provider-side/unisolated failure, no positive livequeryacceptance.
- Live query initially403 because .NET lacked User-Agent. Controlled .NET empty-query probe: absentUA403HTML vs Marketplace-Sandbox/1.0 UA200JSON. Added transparent UA+Accept headers with RED→GREEN. Query now reaches provider but returned unsigned code99 Unexpected error; no DB mutation. Empty/zero optional transaction-number probes did not fix99. Diagnose actual API/merchant setup, preserve guards.
- Public bridge: https://sun-machine-jersey-skip.trycloudflare.com ; only landingGET / and signedGET /api/payments/vnpay/ipn forwarded to API5215; Admin/products paths404 externally. Portable cloudflared2026.11.0 official SHA256 verified. Bridge outside repo in evidence, self-testPASS. Sessions kept: bridge86565, tunnel97870; API39463; Vite36089. Quick Tunnel is temporary, update registeredIPN if it changes.
- All five dedicated registration/merchant/SIT/customer/admin browser sessions closed; two local auth-vault profiles removed. No cookies/headers/HAR export. Private VNPAY credentials remain local only. Direct TerminalEdit.htm without selection was not saved; actualIPN configured/readback on matchingSITterminal.
- FinalDB: Orders5007,Payments4706,Refunds1Rejected,OrderItems12507,16migrations; Product499stock37 due retainedOrder5006pending+Order5007paid. All pre-VNPAY Orders/Payments/OrderItems hashes match baseline. No newDB/schema/seed/restore/backups/originalworkspace changes. Current report/manifest/source hashes: C:/Users/HUY/Documents/Marketplace-Local-Completion/20261115/owner/vnpay-20261115/ . Docs: docs/VNPAY_SANDBOX.md and docs/tech_specs/2026-11-15-vnpay-sandbox.md. No commit/push/PR/merge.

## Owner audit checkpoint retained

- Active scope: Owner DB/business only. RAG and recommendation/classification await corrected AI lanes; no AI source imported.
- Local branch: codex/marketplace-owner-local-20261115, based on main 540d296836be789a58ede9db1039447d95a3c9cc. No remote push/PR/merge.
- Existing DB: (localdb)\MarketplaceRefundDev20261113 / ECommerceDb_Dev; all 16 migrations match source, no pending model changes. No new DB/instance, reseed or restore.
- One source audit found/fixed seller payment leakage, inconsistent cent allocation, hidden-product reviews, concurrent cart increments/inserts and seller fulfillment controls during holds. Also fixed cart integer overflow and StartupSafety test isolation; patched frontend lockfile dependencies.
- Verification: backend 159 passed/3 disposable-only SQL facts skipped; frontend 6 passed, build/lint passed; npm audit 0 vulnerabilities. Fresh SQL probes cover existing-cart increments, first cart/item races and snapshot-trigger rollback. These probes are not substitutes for refund/callback concurrency facts.
- Actual browser: Admin login/dashboard/catalog, Customer add-to-cart/COD checkout/cancel, Seller own-order view. Dedicated browser/auth vault profiles cleaned.
- Audit fixtures retained: Customer1022, Seller1023, Orders5001(cancelled COD),5002(paid demo),5003(cancelled COD). Product499 stock39 from40 due one retained demo purchase; other stock/SKU unchanged. Temporary catalog/coupon fixtures removed through their APIs.
- Online gate retry, 2026-11-15: Owner approved fixed 25637 VND/USD for future transactions. Saved in private Local.json, inherited existing sandbox credentials, restarted API. Browser checkout5004 froze USD505.37 as VND12956171, but payment initiation returned non-JSON; separate GET/empty-POST sandbox probes returned HTTP403 HTML. No successful provider capture was established. Browser cancellation restored Product499 stock38→39; retained Order5004 Cancelled/Payment Failed and immutable reference for the audit trail. No fake capture, refund or external transfer was created.
- Focused payment/refund tests: 38 PASS/0 FAIL/0 SKIP (existing built test assembly, --no-build). Live invalid IPN/callback and demo refund guards PASS. Positive capture/duplicate authenticated callback and refund hold/reject/complete on SQL remain BLOCKED. Refund is the existing manual external-reference MVP, not an automatic MoMo refund integration.
- App URLs: frontend localhost:5173, API/Swagger localhost:5215/swagger. Use the private gitignored Local.json; startup never migrates/seeds.
- Original D:/Dev/Workspaces/E-Commerce and 138 protected files, DbSeeder.cs, V1/V2 backups preserved.
- Report/evidence: C:/Users/HUY/Documents/Marketplace-Local-Completion/20261115/owner/audit-20261115/REPORT.md.
- Online gate evidence: C:/Users/HUY/Documents/Marketplace-Local-Completion/20261115/owner/online-gates-20261115/REPORT.md. Pre-existing Order/Payment/OrderItem row hashes match before the retry; 16 migrations remain unchanged. No new DB, reseed, restore or source logic change.
- Read-only 403 follow-up: documented sandbox DNS/TLS verified, no configured HTTP proxy, User-Agent variants still403. One signed provider status query for existing Order5004 also403 HTML; remote capture remains UNKNOWN. No create retry, new fixture, DB/config or application-code change. Owner has shared demo only; all three configured demo fields match MoMo's official payWithMethod CollectionLink sample. Own M4B is an option, not a proven prerequisite for resolving403. Next discriminator: Owner-controlled network comparison or MoMo denial details; do not assume credentials are invalid or bypass guards.
- Historical comparison: source caa8f97 and current HEAD have identical base MoMo credentials/endpoints/payWithMethod/create-HMAC code. July payment success is reported by Owner and historical project docs (ab571d2); old private runtime/network config is UNKNOWN. Current quote passes; do not attribute provider403 to new callback/refund guards without evidence.
- Follow-up correction: MoMoProvider now reports HTTP failures before JSON parsing and uses a stable invalid-JSON error. No retry/guard/currency changes. Transport regression RED3fail/1pass→GREEN4pass; focused42pass; fresh solution build0warnings/errors, full163pass/3disposable-onlySQLskips. API restarted; no new payment or DB write. Chrome documentation page, native curl and Windows PowerShell status probes also403HTML. Owner has no alternate network now; hotspot comparison NOT RUN. External Test-MoMoConnection.ps1 and unsent MOMO_SUPPORT_REQUEST.md prepared. Root read actual diff/logs; independent bounded review found no concrete defect. HTTP fix/tests/docs remain uncommitted; online gates still BLOCKED.

Historical checkpoint below is retained; older dates/results do not describe current runtime.

---

# Task Checkpoint — MiniShop

Cập nhật: 2026-07-05

## Mẻ hiện tại (2026-07-05, CHƯA COMMIT) — `docs/tech_specs/2026-07-05-marketplace-ui-momo-docs.md`

| # | Task | Trạng thái | Ghi chú |
|---|------|-----------|---------|
| 1 | UI marketplace revamp | ✅ | Palette xanh, header 2 tầng, HomePage (hero/voucher/flash-sale/top-rated/low-stock), Footer link thật, emoji→lucide, max-w-7xl, grid 5 cột |
| 2 | Thanh toán MoMo cổng thật | ✅ | `MoMoProvider` AIO v2 sandbox (HMAC-SHA256, payWithMethod, USD→VND), callback + IPN. Gỡ sạch Stripe + VNPay |
| 3 | Backend hỗ trợ homepage | ✅ | `GET /coupons/active` (public), sort `rating`+`stock` cho ProductService |
| 4 | Gom diagram vào `docs/diagrams/` | ✅ | git mv 6 sơ đồ; sync cross-ref (SRS/DECISIONS/README) |
| 5 | Thêm phân tích bài toán | ✅ | `docs/phan-tich-bai-toan.md` (5 vấn đề cốt lõi, stakeholders, 3 máy trạng thái, scope) |
| 6 | Thêm Component/Deployment diagram | ✅ | `docs/diagrams/architecture-diagrams.md` |
| 7 | Sync docs VNPay/Stripe → MoMo | ✅ | SRS, use-cases, sequence, state-machine, BAOCAO, README |
| 8 | Vá class-erd thiếu | ✅ | Thêm entity `RefreshToken` + enum/field `SellerStatus` |

## Verify (mẻ này)
- `dotnet build ECommerce.API`: 4 project, 0 lỗi/cảnh báo
- `npm run build` (client): sạch
- Smoke test API: `pay {momo}` → `requiresRedirect=true` + payUrl `test-payment.momo.vn`; `pay {vnpay}` → 400 (đã gỡ)
- Thanh toán thật MoMo: chọn Ví MoMo → cổng → thẻ ATM test `9704 0000 0000 0018` → order Paid

## Mẻ trước (2026-07-03, ĐÃ COMMIT `caa8f97`)
Seller onboarding (Pending→duyệt), refresh token (15m access + 7d rotate), rate limiting, integration test (SQLite in-mem, 13 test), email mock, upload MIME sniff, trang shop công khai, payment split, Swagger ProducesResponseType. Đặc tả: `docs/tech_specs/2026-07-03-batch-features.md` + `2026-07-03-hardening-batch2.md`.

## Treo (chưa fix)
- MoMo IPN không tới `localhost` — dev chốt đơn qua redirect callback; deploy public/ngrok mới chạy IPN. Chi tiết: `docs/tech_specs/2026-07-05-marketplace-ui-momo-docs.md`.
- Payment split: mới breakdown kế toán, chưa payout thật.
- Flash-sale "giá gốc" là tổng hợp (×1.4) — app chưa có field giá gốc.

## Còn lại
| Task | Trạng thái | Ghi chú |
|------|-----------|---------|
| Commit mẻ 2026-07-05 | ✅ | UI + MoMo + docs reorg |
| Push GitHub + CI thật | ✅ | Agent đã push |
| Dọn `commit.txt` staged | ✅ | Đã xoá file nháp |

## Tài khoản demo (sau seed)
- Admin: `admin@shop.com` / `Admin@123`
- Customer: `user@shop.com` / `User@123`
- Seller (Approved): `seller1@shop.com`, `seller2@shop.com` / `Seller@123`
- Seller mới đăng ký qua UI = Pending, cần Admin duyệt tại `/admin/sellers`
- Thẻ ATM test MoMo (thành công): `9704 0000 0000 0018`, NGUYEN VAN A, 03/07, OTP bất kỳ

---
Xem thêm: `docs/specs/` (đặc tả từng feature), `docs/tech_specs/` (as-built theo mẻ), `docs/DECISIONS.md` (ADR), `docs/GOTCHAS.md` (bẫy đã dính), `docs/phan-tich-bai-toan.md` (phân tích bài toán), `docs/diagrams/` (UML).
