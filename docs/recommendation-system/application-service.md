# Recommendation Application Service (initial contract)

This note documents the deterministic Application service introduced for Lane B.
The defaults below are implementation proposals only; they are not business facts
and have not been tuned against `HoldoutProductIds`.

## Current branch constraints

The current `khanh/recommendation-v2` HEAD does not contain the V2 structured
catalog contract. Its `Product` entity has category, price, stock, seller and
reviews, but no brand or attribute navigations. To honor the no-schema-change
constraint, this service currently scores by category and relative price only.
Brand and structured attributes remain blocked until the owner-approved EF source
contract is integrated under an explicitly allowed change scope. No schema or
migration files were changed here.

## Purchase eligibility and time boundary

- Eligible parent orders have status `Paid`, `Shipped` or `Delivered`.
- Their one-to-one payment must exist and have status `Completed`.
- Category, product, order, payment, review and order-item rows are included only when
  `CreatedAt < cutoffExclusiveUtc`; the comparison is intentionally exclusive.
- `Cancelled` order items do not contribute to product or category signals.
- `Frequency` is the count of distinct eligible `Order.Id` values.
- `Monetary` sums one `Payment.Amount` per distinct eligible order, in synthetic
  USD. The existing `PaymentService` initializes and confirms payment amount from
  `Order.Total`, which is `Subtotal - DiscountAmount`; this is the working mapping
  assumption. The service does not sum item prices for Monetary and does not
  reconcile or rewrite stored payment values. If imported V2 data violates this
  mapping, owner-provided transaction mapping must resolve the discrepancy before
  claiming Monetary validation.
- `asOfUtc` controls the result timestamp; `cutoffExclusiveUtc` controls all
  catalog and signal reads. Both are service inputs and are not public API
  parameters.

## Candidate and repeat-purchase policy

- Candidate retrieval uses the complete catalog visible before the cutoff and
  requires positive stock.
- Similar-product results exclude the target, discard zero-score candidates,
  apply a minimum score, use score-descending then `ProductId`-ascending ordering,
  and cap each category to the configured diversity limit.
- The explicit repeat policy is `AllowRePurchase = true` by default. Previously
  purchased items remain candidates and receive a `repeat_purchase` reason and
  configured contribution. Setting it to `false` excludes those products.
- Every returned item's reason code corresponds to a positive scoring signal.
- Popular fallback is based on quantities in eligible, non-cancelled order items;
  top-rated fallback is based on reviews before the cutoff. Empty catalog and no
  available evidence return no cards rather than an invented popularity label.

## Current API boundary

- `GET /api/recommendations/personalized` derives the user only from
  `ICurrentUser`/JWT; it has no client `userId` parameter and supports guest
  fallback.
- `GET /api/recommendations/similar-products/{productId}?limit=...` clamps limit
  to 1..20 (default 8).
- Neither endpoint exposes `asOfUtc` or `cutoffExclusiveUtc` query parameters.
- Read-only RFM lives at `GET /api/admin/customers/rfm`; its separate eligibility,
  quintile and segment contract is documented in `rfm.md`.

## Proposed defaults

| Option | Default | Meaning |
|---|---:|---|
| `CategoryWeight` | 0.60 | Category purchase-history contribution |
| `PriceWeight` | 0.40 | Relative price-similarity contribution |
| `RepeatPurchaseWeight` | 0.10 | Explicit repeat-purchase contribution |
| `SimilarCategoryWeight` | 0.60 | Same-category item-to-item contribution |
| `SimilarPriceWeight` | 0.40 | Relative price contribution for similar items |
| `PersonalizedMinimumScore` | 0.25 | Minimum personalized score |
| `SimilarMinimumScore` | 0.35 | Minimum item-to-item score |
| `MaximumRelativePriceDifference` | 0.50 | Maximum price difference divided by reference price |
| `MaximumProductsPerCategory` | 2 | Diversity cap per result set |
| `DefaultResultCount` | 10 | Default maximum result count |
| `AllowRePurchase` | `true` | Explicitly allow repeat recommendations |

These defaults are deterministic starting points only. They must be evaluated on
the development split under the documented protocol and must not be fitted to the
holdout targets.
