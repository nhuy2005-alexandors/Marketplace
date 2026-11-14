# Marketplace backend lifecycle safety — 2026-11-14

This supersedes the lifecycle limitations in the 2026-11-13 Refund acceptance report. Starting source was `d6e4f66f63145be6d6c50050fde178dd8f6dffac` on the existing owner branch. Final source and CI identities are recorded in PR #3. No merge is part of this delivery.

## Implemented contract

### Refund and fulfillment

`Order.RefundPending` is persisted in the same SaveChanges transaction as the refund request. Request, seller shipment, admin status changes, cancellation, rejection, completion and payment writers all update the parent Order row. Its SQL Server rowversion is the common concurrency boundary; an eligibility read followed by a child-only update is insufficient.

- Only the owner of a Paid order with a completed, non-demo, non-COD, non-reconciliation payment and all items Pending can request a full refund. Amount must match the order total.
- Pending hold blocks seller and admin fulfillment and cancellation in the backend.
- Reject is terminal, releases hold and preserves payment, inventory and order status. A valid order may resume fulfillment.
- Complete is terminal, releases hold, refunds payment, cancels order/items and restores stock once, atomically.
- Only actual SQL Server unique violations for the named Refund OrderId/PaymentId indexes map to duplicate-refund errors. Other SQL errors are not reported as duplicates.

### Payment security and reconciliation

MoMo checks the HMAC with constant-time comparison and validates partner, order/request reference, amount range and capture reference. Unsigned/invalid callbacks never reach a database write. Disabled MoMo does not accept demo callbacks or create demo captures. Unknown provider names fail closed.

The application computes a quote without network I/O, then commits a payment reservation and its immutable provider reference/amount/currency **before** calling the provider. A competing initiation must win the same parent/payment rowversion reservation before it can call the provider. Captures have a filtered unique provider/transaction index.

There is deliberately one immutable provider attempt per order. Once a reference exists, even a declined/uncertain attempt cannot be overwritten or retried by Initiate. This avoids losing a later success notification for an old reference. Cancel/reconciliation and a new order are the current recovery options; automatic multi-attempt retry is not implemented.

- Matching duplicate success and late failure cannot regress Completed/Refunded or restock again.
- Capture after cancellation records Completed payment with `RequiresReconciliation`; it does not reactivate the order or change inventory.
- A signed inconsistent capture on a known reference records observed amount/currency/reference in separate reconciliation columns, preserves the frozen quote and any prior terminal capture, and prevents fulfillment/refund/revenue recognition until reconciliation. Nonterminal captures reserve their canonical TransactionId; existing Completed/Refunded IDs remain intact.
- A matching legacy request reference formerly stored in TransactionId can record a signed capture for reconciliation. Historical FX is not invented and fulfillment remains blocked.
- An unknown reference or conflicting unique capture cannot be silently reassigned to another order; the callback returns an explicit conflict for investigation/retry.

Reconciliation is durable and visible in the order DTO/UI. Automated settlement, reconciliation resolution and external refund execution are outside this MVP. The administrator still records a transfer performed outside the application.

### COD and demo

COD initiation moves the order to new status `Accepted` (numeric 5), with Payment Pending and PaidAt null. Seller fulfillment is allowed; cancellation is allowed before fulfillment and cancels the uncollected payment. Shipment/delivery does not claim cash collection. COD collection/refunds remain outside scope.

Mock captures are explicitly `IsDemo`, disabled outside the permitted demo environment, and excluded from Refund eligibility and collected-revenue aggregation. MoMo never silently falls back to a demo capture. Checkout defaults to COD, rather than an implicit mock method.

Historical COD statuses were not converted. A historical Completed COD payment is not assumed to be uncollected and cannot use ordinary cancellation to erase a capture.

### API and UI

The existing refund endpoints remain. `GET /api/refunds/mine` additionally accepts up to 100 repeated `orderIds` query parameters and always applies customer ownership. The Orders UI requests history for its visible five orders, so a refund outside a global first-100 page is not lost. Customer and admin lists have loading/error/empty/retry behavior; pending submission is guarded; terminal admin cards have no processing forms.

Order DTO adds `refundPending`; payment DTO adds `isDemo`, `requiresReconciliation` and reconciliation details. Clients must handle `Accepted`. Consumers must not treat Order Accepted/Shipped alone as collected cash; collected revenue requires a Completed, non-demo, non-reconciliation payment and a valid paid/fulfilled order.

## Schema and development target

Development remains `(localdb)\MarketplaceRefundDev20261113 / ECommerceDb_Dev`, with 15 applied migrations and zero fixture refunds. The canonical V2 backup SHA-256 remains `f5d7444bc36a1fedea7a367ea52d9353d7bf7d020d51530ac9cffe496189a38e`.

| Migration | Purpose |
|---|---|
| Existing `20261111134820_AddStructuredCatalog` | Fresh populated pre-V2 upgrades backfill deterministic unique LEGACY-ID SKUs before creating the unique index. ID/history unchanged; applied V2 databases do not rerun it. |
| `20261113173124_LifecycleSafety` | Parent hold, payment rowversion/frozen attempt metadata and unique indexes; repairs only invalid SKU values, preserves valid ones, and backfills holds for existing Pending refunds. |
| `20261113174420_DemoPaymentIdentity` | Adds IsDemo; identifies only explicit legacy MOCK-/MOMO-DEMO- transaction prefixes. Does not convert money or infer currency. |
| `20261113180756_CaptureReconciliation` | Adds nullable observed amount/currency/reference metadata for reconciliation. |

EF model and snapshot match. Actual EF database-update commands applied the three new migrations to the named development clone. No original database, dataset source, DbSeeder, original dirty worktree or AI branch was modified. No SQL clone was seeded or rebuilt in place.

Design-time precedence: explicit arguments, environment variables, local JSON, environment JSON, base JSON. The factory accepts an explicit isolated LocalDB/ECommerceDb_Dev target and refuses the original MSSQLLocalDB target, missing configuration and ECommerceDb. Runtime environment overrides now have the same precedence over local JSON. An ignored local override keeps the acceptance worktree on the persistent clone.

Example (with the installed SDK/tools on PATH):

```powershell
dotnet ef database update --project src/ECommerce.Infrastructure --startup-project src/ECommerce.API -- --connection "Server=(localdb)\MarketplaceRefundDev20261113;Database=ECommerceDb_Dev;Integrated Security=true;TrustServerCertificate=true"
```

Real MoMo initiation requires explicit `Payment:MoMo:CatalogCurrency` and `CatalogToVndRate`; VND requires rate 1. The legacy UsdToVndRate setting is ignored. Catalog currency/business reconciliation remains OPEN; no historical conversion was performed and no external money was moved in acceptance.

## Verification and limits

| Instrument | Actual evidence |
|---|---|
| Baseline failure reproduction | Five callback/hold/COD assertions failed before fixes; four design-time target assertions failed before configuration fixes. |
| Portable backend | 118 passed, 2 opt-in SQL tests skipped in the ordinary run. Subsequent affected fulfillment/lifecycle regressions: 27 passed after excluding demo revenue; four MoMo verification/configuration cases passed, including normalized VND rejecting a second FX rate. Exact final-commit full CI result is linked in PR #3. |
| SQL Server opt-in tests | Both migration/concurrency tests passed on disposable LocalDB. Includes populated pre-V2 -> current, canonical V2 -> current, preservation of valid SKUs, existing Pending refund hold backfill, request/shipment race, Complete/Reject race, real SQL error rollback, exact duplicate mapping, callback race and a single provider call across competing initiations. |
| Inconsistent signed capture | SQL Server and unit tests verify durable reconciliation, frozen quote preservation, terminal status/ID preservation, and no activation. Real API signed-payload test verifies nonterminal capture identity, metadata and repeated notification idempotency. |
| Browser -> actual API -> SQL Server | Customer request/hold/history, admin hold rejection (409), Reject releasing fulfillment (subsequent status update 200), Complete, terminal card without forms and final customer history passed. No API responses were mocked. |
| Real API verification | Invalid signature 400 with unchanged order DTO, signed success, duplicate success, late failure, cancellation followed by late capture, and matching legacy capture reconciliation passed using synthetic local signing keys. No external MoMo create/charge call was made. |
| Real retry recovery | API stopped to cause Admin list error; restarted API and Retry recovered the actual list. |
| Frontend | Three regression tests and TypeScript/Vite build passed. SSR tests run sequentially to avoid shared HMR ports. |
| Cleanup | API/proxy/browser stopped; both disposable SQL databases/instances dropped/deleted and MDF/LDF absence confirmed. Persistent clone kept. |

Browser form submissions used DOM click/requestSubmit; pointer hit-target reliability is not claimed. SQL Server tests are opt-in because hosted Linux CI cannot run Windows LocalDB. They accept only a `MarketplaceSafetyTest*` instance with ECommerceDb_Dev; the pre-V2 test refuses nonempty migration history rather than downgrading a database.

Local evidence resides under `%USERPROFILE%\Documents\Marketplace-Lifecycle-20261114`. The repository transcript contains only sanitized synthetic fixture output. Initial execution issues (default LocalDB file-path collision, a duplicate synthetic category and concurrent Vite HMR ports) were corrected without weakening integrity or touching source data. Raw test output remains outside the commit.

Independent financial/concurrency review identified reservation, legacy cutover and capture-recording gaps; these were corrected and checked with focused tests. Following the owner's model instruction, the continuing review used GPT-6 Luna Max. No additional architecture or review gate was introduced.

## Post-acceptance hardening addendum — 2026-11-14

**Updated implementation head:** `c3b2d24a66549e95ed25ee49cb3a0530c2abec2c`

Two additional safety gaps were closed after the original lifecycle acceptance:

- Development runtime now validates the database target before service registration. Only a named isolated LocalDB instance with catalog `ECommerceDb_Dev` is accepted; the repository's legacy `MSSQLLocalDB/ECommerceDb` target is rejected. The EF design-time factory uses the same guard.
- Client-provided payment `ReturnUrl` is not forwarded to providers. MoMo uses its server-configured `RedirectUrl`.

Regression coverage:
- Development startup rejects the unsafe repository-default database target.
- A client-supplied external return URL is not forwarded to the payment provider.

CI on the updated implementation head: [run 43589178284](https://github.com/nhuy2005-alexandors/Marketplace/actions/runs/43589178284) — backend 122 passed, 0 failed, 2 opt-in SQL facts skipped; frontend build and 3 regression tests passed.

This addendum changed runtime configuration/payment redirect handling and tests only; it did not change migration source or the accepted database lifecycle SQL behavior. The Windows LocalDB lifecycle evidence recorded above remains scoped to its original tested migration/source revision. The subsequent Owner catalog CI run separately exercises the populated pre-V2 migration and Owner catalog FK/trigger tests.

No persistent development database was migrated by this addendum. Currency remains OPEN, MoMo remains fail-closed until explicit catalog currency/FX configuration, and no external payment/refund was executed.
