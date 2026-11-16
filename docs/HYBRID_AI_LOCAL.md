# Marketplace Hybrid AI — local run

This checkout combines Shared `99d870ad08472d7749c5d0deebbe3dbce725568c` RAG,
chunking, persistent embedding cache and recommendation ranking with Owner
`0e3538bfb8155f036d2a8c819474a15fc850892e` purchase eligibility, RFM and UI.
The preserved local commerce baseline is `de9e2c957b41094ec9668b294649f35b92fcdccb`.
Only one implementation of each AI capability is registered.

## Run on the owner machine

From this repository root, build once:

```powershell
& 'D:\Dev\SDKs\marketplace-dotnet9\dotnet.exe' build ECommerce.sln
```

Start the API in one PowerShell window:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Start-LocalApi.ps1 `
  -LocalConfigPath 'C:\Users\HUY\.codex\worktrees\marketplace-owner-local\E-Commerce\src\ECommerce.API\appsettings.Local.json' `
  -DotnetPath 'D:\Dev\SDKs\marketplace-dotnet9\dotnet.exe'
```

Start the frontend in another window:

```powershell
Set-Location .\client
npm run dev -- --host localhost --port 5173 --strictPort
```

Open `http://localhost:5173`; API/Swagger is `http://localhost:5215/swagger`.
The API launcher requires the existing `(localdb)\MarketplaceRefundDev20261113`
instance and `ECommerceDb_Dev`. It imports private configuration into the child
process. It does not migrate, seed, create or restore a database.
Stop the owned API before rebuilding if Windows reports locked assemblies.

## Private configuration and cache

Keep these files outside Git, under `%LOCALAPPDATA%\Marketplace\AI`:

- `embedding.local.json`: `ApiKey` for native Google `gemini-embedding-001`, 768 dimensions.
- `gemini.local.json`: `ApiKey` and `BaseUrl` for the existing local generation endpoint.
- `hybrid-embedding-cache.json`: persistent content-hash/model/dimension keyed catalog vectors.

Generation currently uses `gemini-3.8-flash` through the existing local proxy at
`http://127.0.0.1:8318/v1`. Its process must be running. Google embedding calls
require network access and available quota. Running the web app on localhost
does not make its AI providers offline.

The first warmup creates only missing vectors and saves each completed batch.
For a free-tier first build, `$env:Gemini__EmbeddingBatchDelaySeconds = '12'`
before starting the launcher reduces bursts. Do not delete the cache to recover
from a provider error. When warmup fails, catalog search uses an explicitly
labelled lexical fallback. Restart the API after provider/quota recovery to
resume warmup. Check `GET /api/assistant/readiness`; only `hybridReady: true`
indicates the semantic index is ready.

## Demo flows and contracts

- Guest: catalog/detail, similar products and `/assistant`; try `Tìm FictionPlay dưới 600 USD`.
- Customer: personalized recommendations, cart/checkout and own order/refund history.
- Admin: `/admin/rfm` for recency, frequency and monetary classification from SQL purchase history.
- Seller: existing own catalog and fulfillment screens remain available.

The assistant accepts `query`, optional `filters` and
`conversation: { conversationId, recentTurns }`. It returns products, sources,
retrieval metadata, typed constraints and context action. Hard filters run in
SQL before recommendation ranking or retrieval. Natural price budgets use USD;
the existing payment conversion policy remains 25,637 VND per USD.

Explicit USD budgets accept `$1000`, `1000$`, `1000 USD`, grouped `1,000$` /
`1.000$`, and decimal `1,000.50 USD` / `1.000,50 USD`. `Trên` / `dưới` exclude
the boundary price; `ít nhất` / `tối đa` and `từ ... đến ...` include it.
Malformed or overflowing amounts are rejected rather than silently dropping the filter.

Vietnamese category requests such as `tìm sách cho tao` resolve through
`AssistantDomain:CategoryAliases` to categories actually present in SQL. Category
filters apply before ranking. Overlapping phrases prefer the specific mention
(`máy tính bảng`); multiple categories or supported direct negation ask for
clarification instead of silently choosing a category.

Gemini selects and orders existing candidate IDs. The backend renders product
names, prices, stock and structured attributes from current SQL DTOs. Unknown,
mixed or empty model selections use the labelled backend catalog fallback;
model prose never supplies catalog facts. No matching SQL evidence remains a
no-evidence response. Account changes cancel or reject previous AI responses,
and private frontend query caches include the user identity.

Recommendation and RFM share purchase eligibility: eligible paid/shipped/delivered
orders require completed, non-demo payments that do not require reconciliation, no pending refund hold
and at least one noncancelled item. Historical cutoffs include payment timestamps.
Cancelled items do not add frequency or attribute preference signals; RFM retains
the Owner proportional allocation for partially cancelled orders.

## Preservation and limits

The existing 16 migrations, commerce entities, persistence model, Payment, Refund,
VNPAY and original `DbSeeder.cs` remain unchanged by the AI integration. The original
workspace and its 26 pinned dirty files are preserved. Do not merge whole Shared
branches over this checkout.

VNPAY signed browser Return is read-only; trusted IPN/capture controls payment
completion. The existing unresolved QueryDr unsigned code 99 remains a provider
limitation. Order 5006 must remain Pending until authenticated confirmation;
Order 5007 has a captured sandbox payment. No external refund was performed.
The refund feature retains its existing manual external-reference MVP contract.

Three opt-in disposable SQL migration/concurrency/rollback tests remain skipped
when their dedicated fixture is absent. They must never target this persistent
development database. Local smoke evidence is not production certification.
