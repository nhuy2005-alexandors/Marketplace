# VNPAY Sandbox adapter — approved implementation scope

Owner requested VNPAY as a separate sandbox provider on the current local Marketplace. Preserve MoMo source and all existing uncommitted work; disable MoMo locally while sandbox access is blocked. No production gateway, migration, reseed, new database or automatic external refund.

## Contract

- Reuse IPaymentProvider and PaymentService. VNPAY uses ProviderKey=vnpay and the existing EWallet payment method; no schema changes. Bind callbacks to the stored provider key/reference, frozen amount and VND currency before changing business state. Keep reservation, replay protection, terminal states and reconciliation behavior.
- Catalog amounts stay USD. Configure VNPAY explicitly with the Owner-approved fixed25637 VND/USD; round provider quote to whole VND AwayFromZero, persist it, and encode vnp_Amount as quote×100. Never convert historical rows. Reject overflow/out-of-range values before reservation.
- Generate 2.1.0 payment URL by ordinal parameter sorting, form URL encoding and HMACSHA512. Use trusted server ReturnUrl and socket client IP, not client-provided redirects/IP headers. UTC+7 create/expiry dates. Reserve the alphanumeric reference `orderId x UTC-yyyyMMddHHmmss x GUID-N` (without spaces) so query can reproduce vnp_TransactionDate without a migration. QueryIpAddress is separate server-owned configuration.
- Return URL verifies the signed payload but never changes financial state; redirect to Orders with a pending-verification message. Only verified IPN or authenticated server-to-server query may call the shared capture transition.
- GET IPN rejects repeated query keys before dictionary conversion, bad signatures/merchant/reference/amount/status fields. Reply using VNPAY RspCode/Message; acknowledge exact duplicates and committed reconciliation observations with00, validation failures97, unknown order01, transient/conflicting/nonterminal results99. Preserve signed inconsistent/late capture evidence for reconciliation and block fulfillment/refund. Only status00 plus ResponseCode00 confirms success; statuses02/09 are final failure, other statuses remain unconfirmed without mutation.
- POST query is authenticated and limited to the order owner/Admin; take reference/date from the stored attempt, sign the querydr request and validate the separately specified response HMAC, merchant/reference/type/status. Unavailable, invalid or nonterminal query responses must not modify payment. Apply verified final results through the same capture code used by IPN.
- Keep refund MVP manual with verified frozen provider evidence; no external refund request is sent.
- Frontend offers VNPAY Sandbox, keeps demo/COD and disables MoMo selection. Missing credentials fail closed. No test or default keys copied into source.

## Verification

TDD covers golden URL/signature encoding, amount scaling/rounding/range, authentication and field tampering, Return read-only behavior, query signing/verification/failure, stored provider binding, duplicate/late/mismatched capture and ownership. Run existing backend/frontend commands and a real local API/browser smoke without claiming provider acceptance from dummy credentials or simulated responses. Live sandbox success remains unverified until real sandbox credentials and reachable registered HTTPS IPN are available.

Sources: https://sandbox.vnpayment.vn/apis/docs/thanh-toan-pay/pay.html and https://sandbox.vnpayment.vn/apis/docs/truy-van-hoan-tien/querydr%26refund.html (downloaded from this machine). Registration is Owner-controlled; do not send email or submit legal/account details without explicit input.
