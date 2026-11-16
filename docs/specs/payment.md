# Payment and Order Financial Lifecycle

**Updated:** 2026-11-15

**Status:** Owner-approved currency policy and current implementation contract

## Canonical currency

USD is the canonical Marketplace catalog currency.

- Product prices, cart subtotals, OrderItem unit prices/subtotals, order subtotal/discount/total, Payment.Amount, Refund.Amount, fixed coupon amounts/minimums, seller split values and dashboard revenue are USD.
- Percentage coupon Value is a percentage, not a monetary amount.
- Monetary DTO fields are USD unless explicitly named as provider-native fields.
- No per-row catalog currency column is needed while the Marketplace has one canonical currency. The USD invariant is enforced by application/configuration contracts and documented API semantics.

## Payment attempt

- One immutable Payment attempt is reserved per Order.
- Payment.Amount remains the canonical USD order amount.
- ProviderAmount/ProviderCurrency freeze the provider-native quote/capture. For MoMo, this is VND.
- Provider callback identity, amount and currency are checked against the frozen reservation.
- Duplicate, delayed and out-of-order callbacks must be idempotent and must not resurrect a cancelled order.
- RequiresReconciliation is fail-closed: unresolved provider/database mismatch is excluded from revenue and verified-purchase eligibility.
- The attempt is not overwritten or silently retried. A failed/uncertain attempt requires a separately approved retry design; it is not implemented by mutating the existing record.
- Provider HTTP failures report the numeric status without exposing the response body. Invalid JSON reports an unconfirmed initiation; neither response confirms a charge or permits retrying the reserved attempt.

## Provider behavior

- Mock payments are demo-only and set IsDemo=true. They are excluded from revenue, refund eligibility and verified-purchase reviews.
- COD creates an Accepted order with Payment Pending. It is not revenue and does not qualify as a verified purchase until a cash-collection workflow marks payment Completed. That collection workflow is not currently implemented.
- MoMo AIO v2 uses signed callback verification and accepts USD catalog orders only. It converts the USD order amount using an explicit `Payment:MoMo:CatalogToVndRate` expressed as VND per USD.
- VNPAY Sandbox is an independent provider (`vnpay`) using the same immutable attempt and capture lifecycle. It freezes whole-VND quotes with an explicit USD-to-VND rate, signs sorted form-encoded2.1.0 parameters with HMACSHA512 and encodes wire amount as VND×100. Its Return URL is read-only; verified IPN or authenticated server query confirms payment. See `docs/VNPAY_SANDBOX.md` for setup and verification limits.
- `Payment:MoMo:CatalogCurrency` is explicitly `USD`. On 2026-11-15, Owner approved the fixed rate **25,637 VND per USD** (`CatalogToVndRate=25637`) for future transactions, not only sandbox tests. The current machine stores this in the gitignored `appsettings.Local.json`; there is no repository-wide implicit rate. Missing or invalid configuration still fails closed.
- The legacy `UsdToVndRate` setting is ignored and is not a fallback. No implicit conversion is allowed.
- MoMo quote is rounded to whole VND using AwayFromZero and checked against the provider's 1,000–50,000,000 VND limits. The quote is stored before provider I/O; callbacks must match it exactly.
- No historical monetary rows are converted, and existing provider quote/capture snapshots retain their original amount/currency. DbSeeder and synthetic Dataset V2 values are USD. Legacy records with unknown original currency remain unverified until their provenance is established.

## Refund

- Refund MVP is full-order only and Admin-recorded.
- `Refund.Amount = Payment.Amount = Order.Total`, all in USD. It is the catalog-denominated refund obligation.
- The original MoMo provider amount/currency remains frozen on Payment (VND); it must not be recomputed using a later FX rate.
- Customer can request a refund only for their own eligible online-paid order before fulfillment begins.
- Admin must record the external transfer/reference when completing a refund.
- Current MVP stores the external reference/note but not the actual external payout amount/currency. Admin must verify the original provider capture evidence before confirming a manual transfer. This limitation is explicit; the application does not assert that the external transfer amount was validated.
- Refund completion atomically updates Refund, Payment, Order, pending OrderItems and stock. Rejection does not mutate payment/order/stock.
- No partial refund, COD refund, automatic MoMo refund API or refund after fulfillment begins.

## Development and safety

- Development runtime and EF design-time require a named isolated LocalDB instance and `ECommerceDb_Dev`; the legacy `MSSQLLocalDB/ECommerceDb` target is rejected.
- No persistent development database or original database is migrated as part of this currency decision.
- No external payment charge or external refund is made during acceptance.
