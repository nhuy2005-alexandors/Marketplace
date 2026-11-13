# RAG implementation log

This file records the baseline, decisions, code changes, and verification performed for the RAG remediation. Update it in the same change whenever implementation files are changed.

## Baseline

- Repository: `nhuy2005-alexandors/Marketplace`
- Branch: `khang/ai_rag`
- Base commit: `df8bd2fc8f935884eaab9e4d3ca4a2d427956439`
- Integration worktree: `D:\NguyenLyHeQuanTriCoSoDuLieu\Marketplace-khang-ai_rag-integration`
- Initial worktree: clean, tracking `origin/khang/ai_rag`
- Initial source findings: AssistantController owns Gemini HTTP calls and accepts client-selected ProductIds; browser `ragService.ts` retrieves the whole catalog and ranks locally; no backend retrieval service or AI-specific limiter exists on this branch.
- Database boundary: do not run the API against the owner's LocalDB; startup invokes `DbSeeder.SeedAsync`, which runs migrations and seeding.
- Schema boundary: integrate the owner-provided schema contract as a complete commit; never apply its migrations to the owner's DB as part of this work.
- Recommendation boundary: no scoring/UI changes; shared contract must be isolated and documented.

## Decisions and gates

- Work in this separate clone; preserve the original workspace copy.
- Route A selected by owner: use a separate integration branch based on `df8bd2f` and merge the complete schema-contract commit `33f054357e57bd37febc3df1f5858527d4201736` (not cherry-pick partial entities).
- Owner approved Gemini Embedding API for the fictional V2 catalog on 2026-11-12. Use the stable text model `gemini-embedding-001`, retrieval task types, 768 dimensions, and never send customer profile/history data. No Gemini API request has been made during implementation.
- Recommendation integration: use only a narrow provider port/stub until the shared contract is available; do not duplicate ranking.
- Evaluation labels remain predicate-derived and NOT_REVIEWED until human review. Keep holdout isolated from tuning.

## Change entries

### 2026-11-12 — K0 baseline and change tracking

- Added this log before implementation changes.
- Commands/evidence: `git status --short --branch` => clean `khang/ai_rag`; `git rev-parse HEAD` => `df8bd2fc8f935884eaab9e4d3ca4a2d427956439`; `git log -1 --oneline` => `feat(ai-rag): improve scope handling and server-side Gemini config`.

### 2026-11-12 — K1 through K4 implementation

- Added Application AI request/response/source DTOs and service/provider interfaces in `src/ECommerce.Application/DTOs/AI/AssistantDtos.cs` and `src/ECommerce.Application/Interfaces/IAssistantServices.cs`.
- Replaced the controller's direct DB/Gemini logic with a thin validated endpoint. Added query/body/context/filter bounds, ignored client product IDs as authority, and applied the `ai` fixed-window rate-limit policy (12 requests/minute/IP).
- Added backend semantic/hybrid candidate retrieval over current SQL products with hard category/seller/price/stock filters; normalized Vietnamese text; only parsed explicit currency amounts; chunked summary and description separately; added content hash/model version/source metadata and bounded in-memory embedding cache.
- Added Gemini embedding/generation adapters with server-side options, named client, timeout, bounded batches/output, safe provider errors, PII/credential redaction, JSON source IDs, and source-ID validation before rendering backend Product DTOs.
- Limited concurrent Gemini HTTP calls to four per process and made an explicit provider no-evidence decision return no cards rather than falling back to candidate cards.
- Added lexical fallback when embedding or generation is unavailable and an explicit no-evidence response. Current Product price/stock are returned from live SQL DTOs; generated text is instructed not to repeat price/stock facts.
- Added a no-op Recommendation provider port. No recommendation scoring/UI/service files were changed. The port must be replaced against Khánh's accepted shared service contract before cross-lane Recommendation behavior is considered complete.
- Replaced the browser catalog download/local product ranking with one backend request; fail closed with a text-only error. Added bounded conversation context, conversation reset on account change, and ref-based custom-event handler.
- Preserved Recommendation's existing text-normalization dependency by extracting the existing exports into `client/src/services/textNormalization.ts` and re-exporting them; no Recommendation scoring code changed.
- Added deterministic unit and SQLite in-memory integration tests in `tests/ECommerce.Tests/Unit/AiRagRemediationTests.cs` and `tests/ECommerce.Tests/Integration/AssistantEndpointTests.cs`.
- The integration branch now includes the complete V2 schema contract: Brand/SKU and typed attribute entities plus its four EF migrations. The migrations add catalog and commerce constraints and are guarded for the named local development DB. API startup no longer auto-migrates/seeds. No API, migration, DB, or LocalDB was run.
- Updated catalog chunk assembly to read current SQL `ProductAttributeValues` and active definitions. It emits separate summary (name/SKU/brand/category), specifications (typed attribute pairs), and description chunks, with SHA-256 content hash and embedding model version. Price and stock are deliberately excluded from embedding text and remain live SQL DTO data. No customer/profile/order data is sent to the embedding provider.
- Structured attributes are formatted by their declared type using invariant decimal formatting. The current schema rows are fetched at search time, not materialized into a persistent vector store.

### Verification record

- `git status --short --branch` at initial baseline: clean branch `khang/ai_rag`; HEAD remains `df8bd2fc8f935884eaab9e4d3ca4a2d427956439` (no commit created).
- `dotnet restore ECommerce.sln --disable-parallel -m:1`: exit 0. NU1900 warnings because `https://api.nuget.org/v3/index.json` vulnerability metadata could not be reached; package restore completed from cache.
- `dotnet build ECommerce.sln --no-restore -c Release -m:1`: exit 0, 0 errors. NU1900 warnings as above.
- `dotnet test tests/ECommerce.Tests/ECommerce.Tests.csproj --no-restore -c Release -m:1 --filter 'FullyQualifiedName~AiRagRemediationTests'`: exit 0, 8 passed, 0 failed at that checkpoint.
- Final `dotnet test ECommerce.sln --no-restore -c Release -m:1`: exit 0, build succeeded, 89 passed, 0 failed, 0 skipped. Run with approved elevated process access because the sandbox testhost stalled before discovery.
- `npm ci`: exit 0; npm reported 12 dependency audit vulnerabilities (2 moderate, 10 high). No dependency update/audit fix was performed.
- `npm run build`: exit 0 (TypeScript + Vite production build).
- `npm run lint`: exit 0.
- `git diff --check`: exit 0; Git emitted only LF-to-CRLF normalization warnings for two edited files.
- No frontend unit test runner exists. No live Gemini smoke test, Windows localhost owner-DB run, or holdout evaluation was performed. The bundled V2 query fixtures are not present on this GitHub branch; evaluation metrics were not reported.

### 2026-11-13 — Integration branch and V2 chunk types

- Fetched and verified `origin/codex/marketplace-v2-schema-contract-20261112` at `33f054357e57bd37febc3df1f5858527d4201736`; its merge base with Khang's lane is `ab571d20ae738d0d8cae0b3feceaced508c4013a`.
- Created branch `integration/khang-rag-v2-contract` in the separate integration worktree from `df8bd2f`, then merged the full schema-contract commit with a merge commit. The original dirty working clone was left intact.
- Ported the existing uncommitted RAG remediation changes from the working clone into the integration worktree, then extended `SemanticSearchService` for V2 Brand/SKU and typed attribute specification chunks. No changes to recommendation scoring were made.
- Remaining gates: check the actual owner's `ECommerceDb_Dev` migration history/schema and approve any migration run separately; ensure V2 product import completed; add persisted/offline chunk and embedding generation if required for repeatable evaluation; validate chunk counts/content from the actual DB; run retrieval and isolated 96-dev/24-holdout evaluation with human-reviewed labels; smoke-test Gemini using a newly rotated server-side key; finalize Khánh's shared Recommendation contract and same-commit integration; then run the agreed checks and record results here.
- The API key pasted in chat must be revoked/rotated before any live provider request. It is not stored in this repository or used by the implementation.
- Integration HEAD after the schema merge: `2a87e3aac585d657431e8888db75c8b2730293b1` (worktree changes remain uncommitted).
- `dotnet restore ECommerce.sln --disable-parallel -m:1`: exit 0; NU1900 warnings because vulnerability metadata at NuGet.org was unreachable.
- `dotnet build ECommerce.sln --no-restore -c Release -m:1`: exit 0, 0 errors; same NU1900 warnings. `git diff --check`: exit 0. Tests were not run after integration in this worktree.
- On 2026-11-13, after the owner configured the new key in repo-root `.env`, a synthetic one-item embedding smoke request succeeded with 768 dimensions. No real catalog or personal data was sent. Two synthetic generation calls to the configured `gemini-3.8-flash` endpoint timed out (30s, then 60s with low thinking); generation remains unverified and the app's 20s HTTP timeout needs review before calling this ready.
- Full integration tests were run after the key was present: the first attempt revealed that `CustomWebApplicationFactory` inherited the root `.env` key, causing tests to call Gemini and making unmatched-query results nondeterministic. The test host now post-configures the API key to empty. Re-run result: 92 passed, 0 failed, 0 skipped. No live DB was used by tests.
- Frontend verification after `npm ci`: `npm run build` and `npm run lint` both passed. npm reported 12 audit findings (2 moderate, 10 high); no dependency changes were made to address them.

### 2026-11-13 — Owner restore, read-only data check, evaluation attempt

- Reviewed `Program.cs`, `DbSeeder.SeedAsync`, and migration attributes without starting the API. `Program.cs` does not call the seeder or `MigrateAsync`. The assembly contains exactly these 11 IDs, matching the restored DB history: `20260629115014_InitialCreate`, `20260629123220_AddCouponsAndDiscounts`, `20260702041438_AddSellerMarketplace`, `20260702051127_AddOrderItemFulfillment`, `20260702065719_AddConcurrencyTokens`, `20260703133450_AddSellerApprovalStatus`, `20260703141211_AddRefreshTokens`, `20261111134820_AddStructuredCatalog`, `20261111135245_AddCommerceIntegrity`, `20261111135534_RequireNonblankSku`, `20261111135643_PortableSkuConstraint`.
- `DbSeeder.SeedAsync` itself always calls `MigrateAsync`, then inserts demo users/products/categories/coupons only if their respective tables are empty. It is not invoked by production API startup on this branch; the test factory uses it only against its isolated in-memory SQLite DB. Do not invoke it against V2.
- `git check-ignore -v src/ECommerce.API/appsettings.Local.json` confirmed the local override is ignored. Created that ignored file with Windows-authenticated `(localdb)\\MSSQLLocalDB` / `ECommerceDb_Dev`, no password/key. `Program.cs` loads it after the default configuration. The tracked `appsettings.json` still contains `ECommerceDb` as a fallback if the local override is removed; current local effective config is `ECommerceDb_Dev`. No process-level connection override was present. Never launch without verifying the ignored override remains in place.
- Read-only `sqlcmd` verified selected DB `ECommerceDb_Dev`, instance `LOCALDB#63C91647`, exact 11 migration rows, and tables/counts: Categories 8, Brands 35, AttributeDefinitions 48, Products 500, ProductAttributeValues 3,000. Blank/null SKU products: 0. Products without exactly six attribute values: 0. Products with description: 500. No write query was issued.
- Computed actual chunk text lengths using catalog SELECT data and the code's formatting: summary 500 chunks, min/median/max 95/100.5/103 UTF-16 characters; specifications 500, 81/120.5/141; description 500, 344/367/403. Total 1,500. At 32 items/request, initial document embeddings require 47 batches, then query embeddings. No text or personal data was printed by the measurement command.
- Added exact explicit SKU matching as a SQL filter. This prevents lexical/semantic retrieval from returning arbitrary products for a nonexistent full SKU and supports the V2 exact-SKU/no-result fixtures. Added tests for hit/miss behavior.
- Changed the in-memory vector cache key to `contentHash:embeddingModel:dimensions`, single-flight cache population, batch size 32, maximum two concurrent document batches, and lexical fallback after provider errors. Query vectors are not persisted. Added tests for cache reuse and lexical fallback.
- Set Gemini 3.8 generation thinking to `low` per current Google API guidance. Kept the 20-second server HTTP timeout because direct generation still did not return within a 60-second synthetic probe; increasing the timeout would delay fallback without a successful response.
- Synthetic Gemini embedding smoke: one document request succeeded, 768 dimensions. Synthetic Gemini 3.8 generation with low thinking: HTTP status unavailable (no HTTP response), `TaskCanceledException` at 20,034 ms; earlier 30s and 60s probes also timed out. This was a timeout, not an observed 429 or blocked HTTP response. No raw body/prompt/key was logged. Generation remains unverified; the server falls back to a local grounded response when generation fails.
- Added author-written K-03/K-05 fixture at `docs/rag-evaluation/RAG_AUTHOR_WRITTEN_K03_K05.jsonl` (five English-only/paraphrase queries; predicate labels over fictional catalog; not human-reviewed).
- Hybrid evaluator `powershell -NoProfile -File .\\tools\\rag\\evaluate-v2.ps1` stopped on its first 32-item embedding batch with HTTP 429. It stopped before loading/evaluating the fixture split; no holdout query was evaluated and no threshold was tuned from holdout. The 8-item external diagnostic did not return a usable result and is not counted as a successful probe. Do not retry bulk embedding until the owner reviews the new Gemini project's quota/limits.
- Development-only lexical sanity run `powershell -NoProfile -File .\\tools\\rag\\evaluate-v2.ps1 -LexicalOnly` (holdout evaluation disabled): 88 relevant dev queries, Recall@5 46.60%, MRR@10 74.56%; 8/8 expected-no-result queries empty after the exact-SKU filter. Author-written K-03/K-05 lexical check: Recall@5 28.10%, MRR@10 100.00% (only five author-written, predicate-derived queries; not a benchmark). Hybrid comparison and all holdout metrics remain unreported because Gemini returned 429 before evaluation.
- Added `docs/rag-evaluation/RECOMMENDATION_CONTRACT_PROPOSAL.md`; it keeps Khánh's scoring, the existing `ProductDto`, and `IRecommendationProvider` port unchanged. It proposes guest behavior without assuming personalization.
- Final verification for this checkpoint: `dotnet restore ECommerce.sln --disable-parallel -m:1` exit 0 (NU1900 metadata warnings); `dotnet build ECommerce.sln --no-restore -c Release -m:1` exit 0 (0 errors, same warnings); `dotnet test tests/ECommerce.Tests/ECommerce.Tests.csproj --no-restore -c Release -m:1` exit 0 (95 passed, 0 failed, 0 skipped); `npm ci` exit 0 (12 audit findings: 2 moderate, 10 high); `npm run build` exit 0; `npm run lint` exit 0. No `npm audit fix` was run.
- `git diff --check` is required after this log update. No production API startup, no migration, no DB write, no push, and no holdout evaluation were performed. Do not call this Localhost PASS or Integrated AI PASS. API startup still needs explicit owner approval; the API key should remain new, private, and in ignored `.env` only.
- `.env` is ignored by Git and its value was never printed. A read-only connection attempt to `(localdb)\\MSSQLLocalDB` / `ECommerceDb_Dev` failed with login timeout; the LocalDB instance registry could not be read in this session. Therefore `__EFMigrationsHistory` and actual V2 table presence remain unverified. No migration or database write was attempted.

### 2026-11-13 — Key refresh, bounded retries/cache and current blockers

- Confirmed root `.env` contains a configured `GEMINI_API_KEY` without printing or copying its value. The earlier key exposed in chat is not used; the configured replacement is used only by local scripts/runtime.
- Sent one synthetic, non-catalog embedding batch of 16 items to `gemini-embedding-001` with 768 dimensions: HTTP 200, 16 vectors, 1,880 ms. This confirms this key and embedding endpoint work at this batch size. No catalog or user data was sent in this probe.
- Synthetic Gemini generation diagnostic: `gemini-3.8-flash` low-thinking returned HTTP 503 after 6.3 s; medium-thinking and streaming probes timed out after 60 s; model-list diagnostic did not complete successfully. Generation is still unverified and may rely on the lexical fallback. No API key, response body, or prompt was logged.
- Added bounded embedding retries for 429/503 (up to 3 attempts, 30-second window, Retry-After or exponential backoff with jitter), serial provider concurrency, and 16-item batches. Added a local atomic JSON embedding cache keyed by content hash/model/dimensions; vectors and catalog identifiers only are cached under ignored `obj/`, no source text or profile/order data. Added retry/cache unit coverage.
- Updated evaluation script to explicitly separate `-DevelopmentOnly` from `-HoldoutFinal`. Holdout now requires a saved dev-only selection file and does not run by default. Evaluator parser check passed. The attempted development evaluation stopped before data retrieval because PowerShell `System.Data.SqlClient` type initialization failed. A direct read-only `sqlcmd` request to `(localdb)\\MSSQLLocalDB` / `ECommerceDb_Dev` then failed with login timeout. Consequently, no catalog embeddings, dev tuning, selection file, or holdout evaluation were generated.
- Gemini probe was run with normal network access after the sandbox blocked the first attempt. This was approved synthetic-only provider traffic; it did not contain database data.
- Full local API startup and guest endpoint smoke tests remain blocked because the required database connection and pre-start DB history/count gate cannot be verified. No API was started. No migration, DB write, login, or holdout request was performed.
- Verification: `dotnet test tests/ECommerce.Tests/ECommerce.Tests.csproj --no-restore -c Release -m:1` passed (100/100); `npm run build` passed; `npm run lint` passed; `git diff --check` passed. NuGet printed NU1900 vulnerability metadata warnings because nuget.org was unreachable. Scanned generated frontend assets against the current key without printing the key: `BuiltBundleContainsGeminiKey=False`.
- Threshold/semantic weight remain the existing runtime values (0.38/0.5). They have not been tuned without dev evaluation data. Do not describe retrieval as hybrid-evaluated, Localhost PASS, or Integrated AI PASS.

### 2026-11-13 — Resumable embedding batches and longer 429 backoff

- Updated `tools/rag/evaluate-v2.ps1` only; the live evaluator was not run. Defaults are 16 items/request, 3 seconds between batches, up to 5 attempts total per batch, fallback delay 15/30/60/90 seconds with jitter bounded by the configured per-attempt ceiling (90 seconds), and at most 300 seconds total retry wait. `Retry-After` delta/date is honored exactly when it fits the remaining total wait; otherwise the batch stops before exceeding the wait budget. These values are configurable through `-BatchDelaySeconds`, `-RetryMaxAttempts`, `-RetryMaxDelaySeconds`, and `-RetryMaxTotalWaitSeconds`.
- After each successful document batch, the evaluator updates the same `contentHash:model:dimensions` cache keys and atomically writes a temporary JSON file then renames/replaces the cache. Subsequent runs filter cached entries before requesting embeddings, so completed batches are resumed. It prints pacing/retry progress with batch number, wait duration and cumulative wait. A terminal document-batch error states how many chunks succeeded during the current run, how many remain, total valid cache entries, status and attempts; it throws and does not continue into evaluation.
- Applied the same retry/pacing behavior to `GeminiAssistantProvider`: configurable options default to 3 seconds between batches, 5 attempts, 15/30/60/90 second fallback waits capped at 90 seconds per wait, and 300 seconds total wait. Provider retries continue to honor `Retry-After`, log only status/timing, and throw a safe provider error at the cap.
- Moved runtime embedding-cache persistence into each successful document batch. If a later batch fails, vectors from earlier successful batches are already atomically present in the ignored local cache.
- Added fake-provider coverage for 429 with `Retry-After`, 429 without the header then success, repeated 429 bounded at five requests, and persisted first-batch vectors when the next batch fails. Evaluator was deliberately not executed; no Gemini requests or DB access were made for this change.
- Verification: .NET tests passed (101/101); solution Release build passed (0 errors); frontend lint passed; evaluator PowerShell parser check passed; `git diff --check` passed. NU1900 vulnerability-metadata warnings remain because nuget.org was unreachable.

### 2026-11-13 — Windows PowerShell 5.1 atomic cache replacement fix

- The previous checkpoint only parsed `evaluate-v2.ps1`; it did not execute the cache replacement path in Windows PowerShell 5.1. That left the PowerShell/.NET Framework `$null` binding bug undetected.
- Extracted file replacement into `tools/rag/AtomicJsonCache.ps1` and changed the `File.Replace` backup argument to `[NullString]::Value`. Both initial creation and subsequent replacements use a same-directory temporary file. If replacement fails, the old destination stays in place and the temporary file is removed.
- Added isolated regression test `tools/rag/tests/Test-AtomicCache.ps1`. It uses no evaluator, network, database, or key; performs three successive cache writes, verifies all three items, injects a missing temporary source during replacement, and confirms the prior cache bytes are unchanged.
- Ran with Windows PowerShell 5.1 (`powershell.exe` 5.1.26100.9711): `powershell -NoProfile -ExecutionPolicy Bypass -File .\\tools\\rag\\tests\\Test-AtomicCache.ps1` => PASS. `-ExecutionPolicy Bypass` is needed in this environment because the machine is `RemoteSigned` and otherwise blocks this local test script.
- Scanned all `tools/rag/*.ps1` for .NET file replacement calls passing `$null`; the only file replace now passes `[NullString]::Value`. Other `$null` uses are defaults, comparisons, or assignments, not nullable .NET string overload arguments.
- No evaluator, Gemini request, DB access, or holdout was run for this fix.

### 2026-11-13 — Safe Gemini quota diagnosis and resumable development evaluator

- Owner-reported prior run state: evaluator cache contained 976 entries; 419 chunks remained; the run stopped at batch 61/87 after all five attempts returned HTTP 429 and about 195 seconds of backoff. This is consistent with a possible daily quota boundary but does not establish that the project exhausted its daily quota; no real Gemini request was made to investigate.
- Added an allowlist quota diagnostic in `tools/rag/EvaluationResilience.ps1`. For HTTP 429 it parses the body in memory and logs only HTTP code, provider status, quota metric/id/dimensions, retry delay, and `per-day` / `per-minute` / `unknown` classification. The raw body, prompts, catalog content, and API key are never emitted. Per-day classification stops immediately. Two already-completed long 429 retry waits (each at least 60 seconds) followed by another 429 also stop with a possible-daily-quota message and saved/remaining chunk counts.
- Added `-MaxBatchesPerRun` (0 means unlimited, the default). A capped run processes only that many missing document batches, atomically saves completed batches, then exits with a resume message before query embedding/evaluation. Added a pre-run forecast for missing chunks, planned/total document batches, unique uncached queries, query batches, and estimated endpoint calls.
- Added evaluator-only query-vector cache at ignored `obj/rag-query-embedding-cache.json`, keyed by SHA-256(query):model:dimensions. Each unique query is embedded once across all threshold trials; successful query batches are atomically cached. Runtime application query-vector behavior is unchanged.
- Added offline Windows PowerShell 5.1 tests with a fake provider: per-day quota stop, per-minute RetryInfo wait then success, query cache hit, and corrupt query cache recovery. Commands: `powershell -NoProfile -ExecutionPolicy Bypass -File .\\tools\\rag\\tests\\Test-EvaluationResilience.ps1` and `powershell -NoProfile -ExecutionPolicy Bypass -File .\\tools\\rag\\tests\\Test-AtomicCache.ps1`. In this environment `RemoteSigned` blocks direct script launch without the per-process `-ExecutionPolicy Bypass` flag.
- This change was verified without running `evaluate-v2.ps1`, making network calls, reading/writing a database, or evaluating holdout. No Gemini request/key was used.
- Final verification for this checkpoint: both PowerShell 5.1 isolated tests passed; parser check passed for evaluator, helper modules and both tests; .NET tests passed (101/101); Release solution build passed (0 errors); frontend lint passed; `git diff --check` passed. NU1900 warnings were emitted because NuGet vulnerability metadata was unreachable. Evaluator, Gemini, database and holdout remained untouched.
