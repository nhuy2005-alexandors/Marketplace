# Read-only RFM endpoint

`GET /api/admin/customers/rfm` returns a paginated, read-only customer RFM view
and global segment counts. It requires the `Admin` role. The response includes
`UserId` and aggregate metrics only; it does not include email, name, address or
other PII.

The endpoint captures one `asOfUtc` for the request and passes that same UTC value
as the exclusive data cutoff. The clock/cutoff values are internal service inputs,
not public query parameters. Paging is clamped to page >= 1 and pageSize 1..100.

## Eligible purchase contract

- Customer users only; users created at or after the exclusive cutoff are excluded.
- Order status must be `Paid`, `Shipped` or `Delivered`.
- A `Completed` payment must exist, and both order and payment `CreatedAt` must be
  earlier than the exclusive cutoff.
- Frequency counts distinct eligible `Order.Id` values.
- Recency is whole elapsed days from the latest eligible `Order.CreatedAt` to
  `asOfUtc`, floored and clamped at zero.
- Monetary sums `Payment.Amount` once per distinct eligible order, in synthetic
  USD. The current `PaymentService` uses `Order.Total` (subtotal less discount)
  when it initializes/confirms payment. This is the implementation assumption;
  imported data should be reconciled with the owner's transaction mapping before
  claiming monetary validation.
- No-purchase customers use segment `NoPurchase` (label `New / no eligible
  purchases`); R/F/M quintiles and recency stay null. Frequency and Monetary are
  zero because there are no eligible rows, not because scores were imputed.

## Quintiles

Quintiles are computed independently for R, F and M over purchasers only. Each
metric is sorted best-first, with `UserId` ascending as the deterministic tie-break:

- R: fewer RecencyDays is better.
- F: greater Frequency is better.
- M: greater Monetary is better.

For `N` purchasers, ranked index `i` receives
`5 - min(4, floor(i * 5 / N))`. Thus 5 is best; for fewer than five purchasers,
some quintile values are naturally empty. `NoPurchase` users do not enter these
rankings. The calculation is request-time only and persists no scores.

## Single segment map

`RfmService.MapSegment` is the sole segment mapping function, applied after all
three quintiles are assigned. First matching rule wins:

| Rule | Segment |
|---|---|
| Any quintile is null | `NoPurchase` |
| R >= 4, F >= 4, M >= 4 | `Champions` |
| R <= 2, F >= 3 | `AtRisk` |
| R >= 3, F >= 4 | `Loyal` |
| R >= 4, F >= 2 | `PotentialLoyalist` |
| M >= 4 | `HighValue` |
| Otherwise | `NeedsAttention` |

These are documented implementation categories, not business facts. The segment
counts cover the complete filtered customer population, while the response items
contain only the requested page. No holdout targets were used to choose these
rules.
