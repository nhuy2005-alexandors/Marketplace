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
- PR #4 is open and Ready for Review, stacked on PR #3 head `4c7c3d8bf977f2b1ca7eec869c57950ec222c963`; it is mergeable and not behind.
- Neither PR was merged. No merge or deployment is authorized by this receipt.
- Khang and Khánh AI branches were not modified, merged, rebased or synchronized.


## Owner UI and refund-auditability addendum — 2026-11-14

**Implementation head tested:** 941c70df9ad00c4e82e7219c44436511b3919b3a  
**CI:** [run 43602730935](https://github.com/nhuy2005-alexandors/Marketplace/actions/runs/43602730935) — completed successfully.

### Completed vertical slices

- Admin and Seller product forms now expose SKU, optional Brand and category-defined typed attributes. Product category remains immutable during edits.
- Admin catalog settings supports Brand create/update/delete and AttributeDefinition create/update/deactivate/delete. Referenced definitions cannot be deleted; deactivation preserves existing product values.
- Public product cards/details expose Brand, SKU and typed attributes. Product listing supports server-side Brand and category-scoped typed attribute filters: exact text, numeric range and boolean.
- Product updates replace the active attribute set but preserve values associated with inactive definitions. Reactivating a definition makes its prior values visible again.
- Fixed coupon value/minimum labels consistently identify USD; Seller Dashboard customer metric label no longer claims a platform-wide count when the value is seller-scoped.
- Refund API now exposes the frozen provider capture amount/currency, payment method, provider key and payment transaction ID. Admin Refund UI displays the USD refund obligation separately from provider-native capture evidence.
- Refund request and completion fail closed if original provider capture amount/currency is unavailable. Actual external payout amount/currency remains outside the current MVP and must be verified by Admin against the external transfer evidence.

### Final CI evidence for the implementation head

| Instrument | Result |
|---|---:|
| Backend build | PASS |
| Backend tests | 150 passed, 0 failed, 3 opt-in SQL facts skipped |
| Frontend type-check + build | PASS |
| Frontend regression tests | 3/3 PASS |
| Windows disposable LocalDB migration/trigger tests | 2/2 PASS |

The two Windows tests exercised the populated pre-V2 SKU upgrade and Owner catalog FK/checkout-snapshot trigger. The separately scoped SQL lifecycle concurrency fact remains opt-in and is not included in this run.

### Boundaries retained

- USD remains the canonical catalog/order/payment/refund currency. MoMo provider-native quote/capture remains VND.
- MoMo FX rate is intentionally unset. Real MoMo initiation remains fail-closed until an FX source/rate is approved.
- No historical monetary rows were converted.
- No actual external payment charge or refund transfer was made.
- No persistent ECommerceDb_Dev or original database was migrated; LocalDB acceptance ran on disposable CI instances only.
- COD collection, reconciliation resolution, automatic provider refunds and multi-attempt retry remain outside this MVP.
- PR #4 remains stacked on PR #3. No AI lane branches were modified, merged or synchronized. Neither PR was merged into main.
