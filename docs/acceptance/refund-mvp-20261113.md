# Refund MVP local acceptance — 2026-11-13

Result: targeted local acceptance passed. This report covers the Refund implementation based on `8fd84836b3e67dd912574d71149f1cb8a69a3df6` and the acceptance fixes in the commit containing this report. PR #3 remains Draft; merge remains an owner decision. CI results must be checked on the exact pushed commit.

## Published fixes

- Seller fulfillment returns Conflict when saving a stale OrderItem raises `DbUpdateConcurrencyException`.
- Admin Refunds displays an error and retry control after a failed list request.
- Regression tests cover exception-to-Conflict handling and rendering the list error. The frontend test now runs in GitHub Actions through `npm test`.

## Canonical baseline and migration

The source baseline was `Marketplace_AI_Development_V2.bak`, SHA-256 `f5d7444bc36a1fedea7a367ea52d9353d7bf7d020d51530ac9cffe496189a38e`, matching its V2 manifest. `RESTORE VERIFYONLY WITH CHECKSUM` passed.

The persistent clone is `(localdb)\MarketplaceRefundDev20261113 / ECommerceDb_Dev`. Restore produced the expected 11 V2 migrations, ending with `20261111135643_PortableSkuConstraint`. The guarded SQL applied `20261112233500_AddRefunds`, producing 12 migrations. No seeder ran and no dataset was regenerated. The existing guard's single fingerprint-property check ran; no full fingerprint or data audit was performed.

The persistent clone has zero refunds. Its API connection was tested with `GET /api/categories` returning HTTP 200. Early connection requests timed out; the subsequent request succeeded. The ignored `appsettings.Local.json` in the acceptance worktree selects this clone.

**Operational limitation:** `DesignTimeDbContextFactory` still hardcodes the original `MSSQLLocalDB / ECommerceDb_Dev` target. The runtime override does not affect that factory. Do not use an unqualified `dotnet ef database update` to migrate the clone. This acceptance used an explicitly targeted, guarded SQL script.

## Verification

| Criterion | Evidence and outcome |
|---|---|
| Backend regression | .NET SDK 9.0.321: 95 passed, 0 failed, 0 skipped. Includes Refund eligibility and cancellation tests. |
| Frontend regression/build | `npm test`: 1 passed. `npm run build`: type-check and Vite build passed. |
| API authentication, roles and validation | Separate SQLite API harness: 2 passed. It covered the five Refund routes, unauthorized/forbidden responses, request validation, request/duplicate, complete/repeat, reject/repeat and state assertions. |
| SQL Server transaction rollback | Real SQL `THROW 51000` injected after an UPDATE during completion. Completion failed; a fresh DbContext read exactly the original stock/order/item/payment/refund state. |
| SQL Server rowversion | Two independent DbContexts loaded the same OrderItem. After one saved, the stale writer raised `DbUpdateConcurrencyException`. |
| Concurrent completion | Two independent DbContexts reached SaveChanges through a barrier for the same Pending refund. Exactly one succeeded and one returned Conflict. |
| No double restock/transition | Stock moved from 5 to 7 for quantity 2; one Completed refund, one Refunded payment and Cancelled order/item persisted. Repeating completion failed and left state unchanged. |
| Real integration smoke | Browser -> loopback HTTP proxy -> actual API -> SQL Server clone. An authenticated synthetic customer session requested two refunds. An authenticated admin session listed requests, completed one and rejected one. Customer subsequently saw both final history states. No API responses were mocked. |
| Persistent state after UI operations | Fresh SQL assertions confirmed the table below and exactly two fixture refunds. |
| Cleanup/preservation | Both disposable test databases were dropped and their instances stopped/deleted. API, proxy and dedicated browser session stopped. Persistent development clone remains available; original worktree, DbSeeder and original databases were preserved. |

SQL tests and real integration used separate disposable instances restored from the same canonical backup and migrated with the same guarded Refund SQL. Synthetic fixtures never entered the persistent development clone. SQL rollback/concurrency checks are local acceptance evidence; they are not part of the portable CI suite.

### Real integration final SQL state

| Fixture order | Refund | Order | Payment | Item | Shared fixture product stock |
|---|---|---|---|---|---:|
| 5001 | Completed | Cancelled | Refunded | Cancelled | 7 |
| 5002 | Rejected | Paid | Completed | Pending | 7 |

Fixture stock began at 5; each order had quantity 2. Reject did not restore additional stock.

Browser navigation and inputs used agent-browser. Some coordinate clicks did not submit; DOM `requestSubmit()` was used for the actual form submissions. This verifies React submit handlers, HTTP calls and persisted state, but does not establish pointer hit-target reliability. Error/retry recovery was separately observed against the isolated mock server; the CI frontend test asserts only error rendering. No external money transfer was performed.

The sanitized request/status and state transcript is in [refund-mvp-evidence.txt](refund-mvp-evidence.txt). Original local logs and disposable harness sources are retained under `%USERPROFILE%\Documents\Marketplace-Refund-Acceptance\20261113-8fd8483\`. Saved state output contains only synthetic fixture state; no shared dataset or real customer records.

## Review and disposition

Independent review found no correctness blocker in the two fixes or CI wiring. The reviewer noted that automated frontend retry interaction coverage can be improved; the current CI test protects error rendering. Source and evidence are ready for owner review. This report does not authorize merging PR #3 or claim general Marketplace/payment correctness beyond these scenarios.
