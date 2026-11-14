# Owner Business + Database Lane — Acceptance Receipt

**Date:** 2026-11-14  
**Repository:** nhuy2005-alexandors/Marketplace  
**PR #3 base:** `4c7c3d8bf977f2b1ca7eec869c57950ec222c963`  
**PR #4 implementation head tested by CI:** `fa73c02c3d166c03cf211078789d3ff7597237c4`  
**Branch:** `codex/marketplace-owner-catalog-integrity-20261114`

## Scope

This work covers the Owner-owned Product/Catalog business contract and database integrity, stacked on the payment/refund lifecycle work in PR #3. It does not coordinate or depend on Khang's RAG lane or Khánh's Recommendation lane.

## Implemented

- Product API/DTO exposes normalized unique SKU, optional Brand and typed structured attributes.
- Admin Brand CRUD and AttributeDefinition CRUD; public read endpoints.
- Product create/update validates brand/category/type/required attributes and typed value exclusivity.
- Product search filters by Brand and typed text/numeric/boolean attributes. Query bounds now match SQL storage precision: Product price `decimal(18,2)`, numeric attributes `decimal(18,4)`, and text filters max 2,000 characters.
- Brand slugs that normalize to an empty value are rejected on create and update.
- Product category and AttributeDefinition category/type are immutable through the application API where they participate in composite keys.
- Product deletion removes dependent structured values first and still respects historical order/cart FKs.
- Public catalog hides products owned by unapproved Sellers.
- Review eligibility requires a Paid/Shipped/Delivered order, Completed non-demo payment, no reconciliation hold, and a non-cancelled matching OrderItem.
- Seller fulfillment validator matches service behavior: Seller can only set Shipped or Delivered.
- Coupon expiration is exclusive at `ExpiresAt <= now`.
- Dashboard revenue and top-product aggregation excludes cancelled OrderItems; seller customer counts are scoped to the seller.
- SQL migration adds OrderItems.SellerId FK, enum check constraints, typed numeric/boolean attribute indexes and immutable checkout-snapshot trigger.
- Development runtime and EF design-time now fail closed unless configured for a named isolated LocalDB instance and `ECommerceDb_Dev`; the repository's legacy `MSSQLLocalDB/ECommerceDb` target is forbidden.
- Payment redirect destinations are server-owned. Client-provided ReturnUrl is not forwarded to the provider; MoMo uses configured RedirectUrl.
- README and SRS/domain specs reflect the current runtime, payment, seller, catalog and database safety contracts.

## Database migration

Migration: `20261114120000_OwnerCatalogIntegrity`

Expected migration count after PR #3's 15 migrations: 16.

The migration is guarded to LocalDB and `ECommerceDb_Dev`. It checks orphan Seller IDs and invalid enum values before adding constraints. It does not seed, import, reset, restore or mutate the V2 dataset.

**No persistent development database or original database was migrated by this acceptance.** Windows CI uses disposable LocalDB instances and distinct MDF paths. The migration was exercised against those disposable databases only.

## Verification — current branch

**CI PASS** Run [43597463025](https://github.com/nhuy2005-alexandors/Marketplace/actions/runs/43597463025), associated with PR #4 implementation head `fa73c02c3d166c03cf211078789d3ff7597237c4`.

| Instrument | Result |
|---|---:|
| Backend build | PASS |
| Backend tests | 147 passed, 0 failed, 3 opt-in SQL facts skipped in Linux |
| Frontend type-check + build | PASS |
| Frontend regression tests | 3/3 PASS |
| Windows LocalDB migration/trigger tests | 2/2 PASS |

The two Windows LocalDB tests cover the populated pre-V2 SKU upgrade and Owner catalog FK/checkout-snapshot trigger. The SQL lifecycle concurrency fact remains a separately scoped opt-in test; the earlier Windows acceptance evidence for that fact is retained and was not represented as part of this CI run.

Additional regression coverage on the current Owner branch:
- Empty normalized Brand slug rejected on create/update.
- Price and typed-attribute query filters rejected when outside SQL precision/scale or text storage limits.
- Client-selected payment return URL not forwarded to the payment provider.
- Development startup rejects the repository's legacy database target.
- MoMo rejects non-USD catalog currencies even when an FX rate is supplied; USD requires an explicit VND-per-USD rate and does not fall back to the legacy 25,000 setting.
- Explicit USD-to-VND quote rounding and provider currency are covered by regression tests.
- Payment.Amount remains USD while the frozen Payment.ProviderAmount/ProviderCurrency quote is VND.

The current PR #3 base head `4c7c3d8bf977f2b1ca7eec869c57950ec222c963` is included in this stacked PR #4 run. The run verifies the shared lifecycle/currency implementation plus the Owner catalog changes.

## Open boundaries

- **USD is the canonical Marketplace catalog currency**, approved by Owner on 2026-11-14. Product, cart/order, fixed coupon, Payment.Amount, Refund.Amount, seller splits and dashboard revenue are USD.
- MoMo accepts USD catalog orders only. `CatalogCurrency=USD`; `CatalogToVndRate` remains intentionally unset because no FX source/rate has been approved. Real MoMo initiation remains fail-closed; legacy `UsdToVndRate` is ignored.
- MoMo provider quote/capture and reconciliation amounts remain VND and are frozen separately from USD Payment.Amount. No historical rows are converted.
- Refund.Amount is the USD full-order obligation. Current manual refund MVP stores only external reference/note and does not persist/verify actual external payout amount/currency; Admin must verify original provider capture evidence before confirming a transfer.
- One immutable payment attempt per order; automatic multi-attempt retry is not implemented.
- Reconciliation resolution, COD cash collection and automatic external refund execution remain future work.
- No external payment charge or external refund was made during acceptance.

## GitHub state

- PR #3 remains open and Ready for Review.
- PR #4 remains open and Draft; it is synchronized on PR #3's latest head and is not behind.
- Neither PR was merged. No merge or deployment is authorized by this receipt.
