# Payment and Order Financial Lifecycle

**Updated:** 2026-11-14  
**Status:** Owner lane as-built contract

## Payment attempt

- One immutable Payment attempt is reserved per Order.
- The attempt is not overwritten or silently retried. A failed/uncertain attempt requires a separately approved retry design; it is not implemented by mutating the existing record.
- Provider callback identity, amount and currency are checked against the frozen reservation.
- Duplicate, delayed and out-of-order callbacks must be idempotent and must not resurrect a cancelled order.
- RequiresReconciliation is fail-closed: unresolved provider/database mismatch is excluded from revenue and verified-purchase eligibility.

## Provider behavior

- Mock payments are demo-only and set IsDemo=true. They are excluded from revenue, refund eligibility and verified-purchase reviews.
- COD creates an Accepted order with Payment Pending. It is not revenue and does not qualify as a verified purchase until a cash-collection workflow marks payment Completed. That collection workflow is not currently implemented.
- MoMo uses sandbox redirect and signed callback verification. Real MoMo is disabled unless catalog currency and the provider FX quote are explicitly configured. No implicit USD/VND conversion or historical-row rewrite is allowed.

## Refund

- Refund MVP is full-order only and Admin-recorded.
- Customer can request a refund only for their own eligible online-paid order before fulfillment begins.
- Admin must record the external transfer/reference when completing a refund.
- Refund completion atomically updates Refund, Payment, Order, pending OrderItems and stock. Rejection does not mutate payment/order/stock.
- No partial refund, COD refund, or refund after fulfillment begins.

## Currency boundary

The synthetic Dataset V2 uses USD as its data-generation convention. Product, Order and Payment monetary values are decimals without a fully adopted persisted catalog-currency contract. Therefore USD in demo data must not be silently treated as the production catalog currency. The Owner must explicitly approve the catalog currency and FX quote source before real MoMo is enabled.
