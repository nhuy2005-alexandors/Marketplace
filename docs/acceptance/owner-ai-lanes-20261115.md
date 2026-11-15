# Owner AI Lanes — Acceptance Receipt

**Date:** 2026-11-16  
**Repository:** `nhuy2005-alexandors/Marketplace`  
**Branch:** `codex/marketplace-owner-ai-lanes-20261115`  
**Base:** `540d296836be789a58ede9db1039447d95a3c9cc`  
**Verified code head:** `8ceca88eeeb2db82e1442c2748d6765ecedc2a27`  
**PR:** #7 — Draft, open, mergeable; not merged  
**CI run:** [43740638782](https://github.com/nhuy2005-alexandors/Marketplace/actions/runs/43740638782) — completed successfully, 3/3 jobs PASS.

## Scope and ownership

This is the Owner-owned backend lane, created directly from `main`. It does not copy, sync, merge, rebase, or depend on Khang/Khánh implementation branches.

The lane adds three separate capabilities:
1. RAG Core / Assistant product search.
2. Recommendation / similar-product ranking.
3. Read-only customer classification (RFM).

RAG may call the shared Recommendation application service for generic recommendation requests. Recommendation and RFM remain independently callable services.

## Implemented contracts

### Recommendation

- Personalization signals come from orders with status Paid, Shipped, or Delivered; payment must be Completed.
- Demo payments, unresolved reconciliation payments, refund-pending orders, and cancelled OrderItems are excluded from purchase signals and global popularity counts.
- Candidate products must be in stock and belong to approved sellers.
- Ranking uses category, brand, comparable typed Catalog V2 attributes, and price only as a refinement signal.
- Previously purchased products are excluded from personalized candidates.
- Similar-product ranking uses category, brand, and comparable attributes; price alone cannot make an unrelated product similar.
- Cold-start strategies are explicitly named `popular`, `top_rated`, or `catalog_fallback`; the implementation does not claim unmeasured products are trending.

### Customer classification (RFM)

- Read-only; no schema or migration changes.
- Customer IDs and aggregate metrics only; names, emails, and other PII are not selected into the classification DTO.
- Eligible orders must be Paid/Shipped/Delivered with Completed payment, non-demo payment, no reconciliation hold, no pending refund, and at least one non-cancelled OrderItem.
- Monetary value is canonical USD. For partially cancelled orders, captured USD payment is allocated pro-rata to non-cancelled line-item subtotal.
- Recency uses the last eligible order. Frequency counts eligible orders. Tied metric values receive the same quintile.
- The `asOfUtc` cutoff checks payment capture time (`PaidAt`, falling back to `Payment.CreatedAt` for legacy rows) so a payment captured after the cutoff is not counted.
- Endpoint is protected by Admin role authorization.
- `asOfUtc` is not a full historical snapshot: eligibility still uses current order/payment/refund states because the schema does not retain historical state transitions.

### RAG Core

- Hard-filtered catalog retrieval over approved sellers using category, brand, seller, price, stock, and typed attribute filters.
- Lexical retrieval is always available. Optional semantic retrieval uses Gemini `gemini-embedding-001` with `RETRIEVAL_DOCUMENT` and `RETRIEVAL_QUERY` task types.
- Embedding dimension defaults to 768; cosine similarity normalizes vector magnitudes during scoring.
- Product documents are SHA-256 keyed after a conservative 1,500-character embedding-input cap; product name, category, brand, SKU, and typed attributes are prioritized before free-form description. The process-local embedding cache is bounded to 1,000 entries.
- Retrieval loads all matching approved-seller products before ranking; there is no fixed 500-product cutoff. A failed embedding batch is logged and skipped without aborting later batches; affected products remain available through lexical retrieval.
- Generation uses stable model ID `gemini-3.8-flash` with a structured JSON response schema.
- Generated source product IDs are validated against backend retrieval candidates. If citations are missing or contain any unknown product ID, generated prose is discarded and backend-grounded fallback is returned.
- If retrieval yields no candidates, the assistant abstains and does not call the generation provider.
- Recent conversation context is bounded to six turns; each turn is bounded to 500 characters. Contextualized retrieval queries are capped at 1,000 characters while preserving the current user query. Follow-up cues use whole-token matching to avoid false positives such as `it` inside `monitor`.
- Public assistant chat has a separate 20-requests/minute/IP rate limit.

## API surface

- `POST /api/assistant/chat` — anonymous or authenticated Customer; 20 requests/minute/IP.
- `GET /api/recommendations?take=...` — public; uses authenticated Customer identity when available.
- `GET /api/recommendations/products/{productId}/similar?take=...` — public.
- `GET /api/rfm/customers?page=...&pageSize=...&asOfUtc=...` — Admin only.

## Configuration and provider boundary

Gemini is opt-in and disabled by default. Local environment configuration may use:

- `Gemini__ApiKey`
- `Gemini__EmbeddingEnabled=true`
- `Gemini__GenerationEnabled=true`
- Optional model/dimension/timeout overrides under the same `Gemini__...` prefix.

No API key or credential is committed. No live Gemini API call was made during acceptance. Gemini HTTP contract tests use a stub `HttpMessageHandler`; live provider authentication, quota behavior, latency, and output quality remain **UNKNOWN** until tested with an authorized key.

The embedding cache is process-local and is rebuilt after API restart. No vector database, Redis, external vector service, schema, or migration was added.

## Frontend integration

- Public route `/assistant` calls `POST /api/assistant/chat`, sends at most six recent turns (500 characters each), and renders the grounded answer, answer/retrieval metadata, and backend-returned product cards.
- Home page consumes `GET /api/recommendations`; the query cache is keyed by authenticated Customer ID so a guest response is not reused as a personalized response after login.
- Product Detail consumes `GET /api/recommendations/products/{productId}/similar` and reuses the same recommendation card section.
- Admin-only route `/admin/rfm` consumes `GET /api/rfm/customers`; it displays segment counts, paged R/F/M metrics, and canonical USD monetary values without customer names or emails.
- The integration reuses the existing Axios client/interceptor, React Query, Zustand auth store, ProductCard, and UI components. No frontend dependency or database/schema change was added.

## Verification evidence

CI run [43740638782](https://github.com/nhuy2005-alexandors/Marketplace/actions/runs/43740638782) completed successfully at code head `8ceca88eeeb2db82e1442c2748d6765ecedc2a27`.

| Check | Result |
|---|---:|
| Backend build | PASS — 0 warnings, 0 errors |
| Backend unit + integration tests | 188 passed, 0 failed, 3 opt-in skips |
| Frontend TypeScript/Vite build | PASS |
| Frontend regression tests | 3/3 PASS |
| Windows disposable SQL Server LocalDB migration/trigger tests | 2/2 PASS |

The three backend skips are opt-in SQL facts not run in the Linux backend job. The separate Windows LocalDB job ran its two disposable-database facts successfully.

### Existing frontend dependency audit signal

The unchanged frontend dependency lockfile was not part of this PR. During `npm ci`, the CI log reported **12 dependency audit vulnerabilities (2 moderate, 10 high)**. This PR did not run `npm audit fix` or change frontend dependencies. Package-level remediation is a separate security task; the finding must not be interpreted as resolved by the successful frontend build.

## Explicit limitations

- No live Gemini API execution or API-key validation.
- No human-reviewed RAG evaluation set, retrieval benchmark, or calibrated ranking weights. Hybrid score weights and thresholds remain deterministic heuristics, not measured quality claims.
- Retrieval currently loads all matching catalog products into memory before ranking; this is suitable for the coursework demo catalog, not a production-scale retrieval design. The process-local embedding cache remains capped at 1,000 entries and resets on API restart; catalogs larger than the cache may evict older vectors and use lexical retrieval for those products.
- Gemini `gemini-embedding-001` has a 2,048-token input limit ([Google documentation](https://ai.google.dev/gemini-api/docs/embeddings)). Product documents are capped at 1,500 characters before embedding, with identity/typed attributes preserved ahead of description. This is a conservative character bound, not an exact provider-token count; exceptionally token-dense text has not been measured against the live API.
- Frontend UI integration is implemented and build-verified. The current frontend regression suite contains three existing tests unrelated to the new AI screens; no browser E2E or local UI runtime smoke has been run.
- No schema/migration, database reset, seeding, import, persistent development database mutation, deployment, or external payment action was performed.

## GitHub and integration state

- PR #7 is open, Draft, and mergeable; it has not been merged.
- PR #6, which was based on the wrong AI integration source boundary, is closed and was not merged. Its branch is retained only as an audit trail.
- `main` remains at `540d296836be789a58ede9db1039447d95a3c9cc`.
- Khang/Khánh branches were not modified, merged, rebased, or synchronized.

**Acceptance boundary:** CI PASS (3/3 jobs) at code head `8ceca88eeeb2db82e1442c2748d6765ecedc2a27`: backend build/tests 188 PASS, 3 SKIP; frontend build and 3/3 existing tests PASS; Windows disposable LocalDB tests 2/2 PASS. Regression tests cover long-document bounding/identity preservation, retrieval beyond 500 catalog rows, continuing after a failed embedding batch, whole-token follow-up matching, and the RFM payment-capture cutoff. Live Gemini behavior, RAG relevance/quality, exact provider tokenization, browser-level frontend behavior, local development runtime health, and production acceptance remain UNKNOWN / NOT ACCEPTED. Keep PR #7 Draft until those boundaries are explicitly reviewed.
