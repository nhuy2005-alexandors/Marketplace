# RAG handoff evidence

**Branch:** `integration/khang-rag-v2-contract`  
**Evidence checkpoint:** owner-reported run on `eb05190`, plus evaluation evidence owner-reported from `3db14e5` and earlier acceptance evidence from `7f01c18`/`69c7fc1`.
**Status values below:** owner-reported results are identified as such; this document does not claim they were rerun in the current sandbox.

## Acceptance summary

| Area | Current status | Evidence / limitation |
|---|---|---|
| Integration | **PASS (owner-reported at `eb05190`)** | Owner reports API build OK and `dotnet test` Passed 129 / Failed 0 / Skipped 0. At `7f01c18`, owner reported 128/0/0. |
| Frontend | **PASS (owner-reported at `eb05190`)** | Owner reports frontend build and lint passed. Command transcripts/artifacts were not provided. |
| Localhost | **PASS một phần** | Guest/no-auth path was tried; authenticated login/account-switching has not been tested. At `eb05190`, owner reports 6/15 intentional rate-limit responses were 429 and the 413 case returned HTTP 413. Full smoke Summary and per-case list were not included in the supplied results, so the overall `eb05190` guest-smoke total is **chưa kiểm chứng**. See the run history below. |
| Database safety | **PASS (owner-reported)** | Counts unchanged across repeated API runs, with SELECT-only logs and zero INSERT/UPDATE/DELETE. Owner supplied the counts below. No DB was accessed for this documentation change. |
| Manual UI | **Owner-reported PASS** | Owner reports the UI was tried and looked good. No screenshots were supplied; account login/switching was not tried. See [MANUAL_FRONTEND_TEST_CHECKLIST.md](MANUAL_FRONTEND_TEST_CHECKLIST.md) for remaining reproducible evidence. |
| Integrated AI | **CHƯA PASS** | Requires Khánh to merge the shared Recommendation contract and the owner to complete integrated acceptance. |

Guest smoke run history (all owner-reported):

| Commit | Result | Meaning |
|---|---|---|
| `69c7fc1` | 15/16 | Sole failure was a script assertion requiring `answerSource` on HTTP 413; API returned HTTP 413 with the expected safe body. |
| `7f01c18` | 15/16 | Sole failure was strict-empty no-result, expected under Direction D because nearby suggestions remain in the response. |
| `eb05190` | Summary/per-case list **chưa được cung cấp**; known observations: 6/15 intentional rate-limit calls returned 429, and payload-too-large returned HTTP 413. | The status of the full suite cannot be inferred from these partial observations. |

## Retrieval and evaluation evidence

Metrics below are those recorded in `RAG_IMPLEMENTATION_LOG.md`; they are offline/cache-based measurements on the synthetic Marketplace V2 catalog, not live production quality guarantees.

| Dataset / mode | Recorded result | Notes |
|---|---|---|
| Development, lexical | R5 46.60%; MRR@10 74.56% | Owner-reported and recorded in the log; 88 relevant queries. In-sample descriptive result. |
| Development, hybrid | R5 55.48%; MRR@10 94.68% | Owner-reported and recorded in the log; threshold 0.59 / weight 0.25 were selected using this same development set, so these are in-sample results. |
| Development, no-result | Earlier lexical run: 8/8 empty | Does not settle live false positives for general nonsense/unrelated wording (K-11). |
| Holdout at `3db14e5` | Lexical: R5(all) 48.80%, R5(capped) 81.82%, ceiling(all) 55.88%, MRR@10 86.36%, no_result_empty 100%. Hybrid: R5(all) 52.45%, R5(capped) 90.91%, MRR@10 92.05%, no_result_empty 100%. | Owner-reported, run exactly once, 24 queries (22 answerable, 2 no-result), threshold 0.59 / weight 0.25 selected from development. Engineering sanity only; limitations below. |
| K-05 at `3db14e5` | Lexical: R5(all) 0.19%, R5(capped) 0.67%, MRR@10 1.67%. Hybrid: R5(all) 4.93%, R5(capped) 17.33%, MRR@10 18.17%, ceiling(all) 28.32%. | Owner-reported for 30 author-written queries, post-SKU-fix; reporting-only and not used for threshold selection. Labels are predicate-derived and unreviewed. |
| No-evidence vs natural-query feature report | Current evidence rule: 18/25 no-evidence weak (72%). Candidate T=0.63 was not applied; it left 13/25 no-evidence strong. | Cache-only diagnostic. Candidate thresholds remain unselected. Scores depend on embedding model and synthetic catalog. |

The development/holdout fixture has 96 rows (88 relevant, 8 no-result); the holdout run used the separate 24-row split (22 answerable, 2 no-result); K-05 has 30 author-written rows. The 175-query golden scoring fingerprint guards ranking output but is not a metric run. After the single holdout run, changes were limited to evidenceStrength/answerSource, wording, and the generation flag; they do not change retrieval ranking. The unchanged golden fingerprint is the regression evidence for this claim. Holdout query counts by type: `category_brand_vi=4`, `exact_sku=3`, `mixed_language=3`, `multi_attribute_vi=4`, `no_result=2`, `product_name=3`, `single_attribute_vi=5`. Owner-reported latency: query embedding mean/p95 1266/1940 ms; ranking mean/p95 66/99 ms.

Owner-reported database snapshot (unchanged over multiple API runs): `Database=ECommerceDb_Dev`, `Migrations=11`, `Products=500`, `ProductAttributeValues=3000`, `Users=1020`, `Orders=5000`, `OrderItems=12500`, `Payments=4700`, `Reviews=1800`, `RefreshTokens=0`. API logs had SELECT only and zero INSERT/UPDATE/DELETE. Owner reports no new migration, no tracked secret file except the sample `appsettings.Local.json.example`, `.env.example` unchanged, and a Git-history scan found no API-key-shaped credential. These are owner-reported; no DB or Git-history scan was performed in this documentation-only update.

## Known limits and risks

- **K-11 is partially mitigated and remains open.** The current evidence rule marked 18/25 no-evidence queries weak (72%); owner reports 4/8 smoke queries were labeled weak. Candidate T=0.63 was not applied because it missed 13/25 no-evidence queries and depends on parameters inspected on a small, unreviewed validation fixture. Direction D retains nearby suggestions; neutral wording does not solve false-positive retrieval.
- **Gemini generation is unverified on the real provider and is disabled by default.** Owner's pre-disable probe found `gemini-3.8-flash` in models.list; generate-low and streaming timed out at 25 seconds, generate-medium returned HTTP 429, and API logs showed a 10-second timeout followed by 429s and an open circuit. There were zero `answerSource="generated"` responses. The handoff product is hybrid search with a template/local SQL-grounded answer; the real Gemini generated-answer path is not verified. Strong answers currently use `answerSource="fallback"` while `Gemini:GenerationEnabled=false`.
- **K-05 is weak, especially for structured numeric attributes.** Correct and incorrect products have close semantic maxima; do not treat K-05 as a tuned benchmark.
- **Weak-evidence wording and author-created evaluation labels are not human-approved.** K-05, no-evidence, and natural-query fixtures need review before being treated as acceptance truth.
- **Holdout is small (24 queries) and has been used once.** A single query contributes about 1/22 ≈ 4.55 percentage points to MRR over the 22 answerable queries. Labels were generated from predicates and have not been human-reviewed. These metrics are engineering sanity checks, not robust estimates. Any future ranking change invalidates this holdout result; report a new evaluation separately and retain this historical result as the `3db14e5` baseline.
- **Gemini key/quota and data terms require owner review.** Current evidence shows quota/timeout constraints. Confirm the selected plan's quota, retention, and data-use terms before sending catalog or user content; keep credentials server-side and rotate any key that was exposed outside the secret store.
- **Manual UI evidence is incomplete.** The owner reported the UI looked good, but no screenshots were supplied. Login and account switching remain untested.
- **Authenticated localhost flow is not covered by the reported guest smoke.** Login/session behavior and role-specific access remain **chưa kiểm chứng** in this handoff.

## Rollback

1. Set server configuration `Gemini:RetrievalSemanticThreshold=0.38` and `Gemini:RetrievalSemanticWeight=0.5` to restore the prior retrieval configuration. Keep these as configuration values; do not edit ranking code to roll back.
2. Set `Gemini:GenerationEnabled=false` to disable only Gemini answer generation; strong-evidence responses use the local grounded answer with `answerSource="fallback"`. Embedding and retrieval remain enabled. This switch is server-side configuration and defaults to false. Set it to true only after separate owner approval and provider acceptance.
3. To revert a deployed implementation change in a shared checkout, confirm the branch and clean working tree, then use `git revert <implementation-commit>` for the specific behavior/configuration commit. This creates an inverse commit and does not rewrite history. Review the diff and rerun applicable tests; do not use `git reset --hard` on a shared branch.
4. For a full rollback to before the RAG lane, revert all dependent implementation commits newest-to-oldest and have the owner review the combined diff. `7f01c18` is the earlier implementation checkpoint; it is not automatically the right single commit to revert for a targeted feature rollback. Retain this evidence document unless it also needs to be removed.

## Owner acceptance steps

1. Confirm the working branch/commit and keep the Gemini key in the server secret store; do not paste it into logs or reports.
2. Verify `ECommerceDb_Dev` identity and record read-only before/after counts and migration history. Start the API only after confirming its migration behavior is safe; verify API logs remain SELECT-only for the smoke scenarios.
3. Complete the `eb05190` guest-smoke evidence by recording its actual final Summary and per-case results; the supplied report currently contains only the 6/15 rate-limit count and HTTP 413 observation. The 413 case should pass on HTTP 413 with a safe error body and must not require `answerSource`; record whether other cases were retried after 429.
4. Run the authenticated user flow separately and capture reproducible notes or screenshots for the manual UI check. Record browser/device, account role, relevant query/filter scenarios, and any failures.
5. Ask Khánh to merge the shared Recommendation contract into the agreed integration commit. Confirm both lanes use the same commit before declaring Integrated AI PASS.
6. Generation is disabled by default. If owner-authorized to probe, run `powershell -NoProfile -File .\scripts\local-acceptance\gemini-generation-probe.ps1 -Models 'model-a,model-b' -Thinking none` (or `low`/`medium` as explicitly selected), review `models.get` capabilities and safe quota fields, and leave pacing enabled. Do not put a key on the command line. Re-enable generation only after the owner approves quota/data terms and a generated response is verified.
7. Complete owner review of weak-evidence wording and author-written labels. Keep K-05 reporting-only; do not tune on holdout.
8. Run the agreed integrated acceptance suite on the final shared commit. Only after guest and authenticated flows, DB safety, provider behavior, and shared contract all pass should the owner change the status to **Integrated AI PASS**.

## Evidence provenance

- Owner-reported at `eb05190`: build OK; `dotnet test` Passed 129 / Failed 0 / Skipped 0; frontend build and lint passed. At `7f01c18`, owner reported 128/0/0. Owner also reports manual UI looked good, no new migration (11 total), DB counts unchanged over repeated API runs with SELECT-only logs, no tracked secret file except the sample `appsettings.Local.json.example`, and `.env.example` unchanged.
- Owner-reported at `3db14e5`: exactly one holdout run with the metrics shown above and one 30-query K-05 reporting-only run. Do not rerun `-HoldoutFinal` merely to reproduce these values.
- Owner-reported smoke: `69c7fc1` 15/16 with the 413 metadata assertion bug; `7f01c18` 15/16 with strict-empty under Direction D; `eb05190` has 6/15 expected 429s in the intentional limiter case and a correct 413, but the owner did not include the final Summary or per-case list.
- Repository-recorded offline measurements: [RAG implementation log](RAG_IMPLEMENTATION_LOG.md), [assistant API delta](OPENAPI_ASSISTANT_DELTA.md), and the fixtures under `docs/rag-evaluation/`.
- Still not supplied: frontend command transcripts, DB log/snapshot artifacts, UI screenshots, full `eb05190` smoke Summary/per-case list, and authenticated localhost acceptance. These do not negate the owner-reported pass results but remain absent as reproducible artifacts.
- The owner has now reported build and .NET tests at `eb05190` after the generation-off change: 129/0/0. This supersedes the earlier note that the new C# unit test lacked owner-side validation.

## Post-`eb05190` scope-guard change

The current integration work adds a deterministic, rule-based RAG intent guard and additive response field `intent`. It is not yet owner-reviewed or integrated with Khánh's lane. Offline PowerShell 5.1 classification over the recorded fixture corpora found zero guarded answerable rows in development (88), holdout (22), K-05 (30), natural queries (40), and answerable smoke cases; the dedicated 80-row authored intent fixture had zero label mismatches. The full list of non-product classifications is in [the intent classification report](rag-evaluation/INTENT_CLASSIFICATION_REPORT_20261114.md). This is not a holdout metric run. The 17 smoke inputs include the original 16 plus one product near-match; the prompt-extraction query is intentionally classified `blocked`.

The rule set can miss unusual wording, K-11 remains partially mitigated, and the authored labels have not been reviewed by a person. “MiniShop” and support wording are provisional. The HttpListener fake API test was attempted in Windows PowerShell 5.1 and skipped because the sandbox denied loopback access. The 175-query golden fingerprint passed unchanged; PowerShell 5.1 local acceptance/fake dry-run and frontend lint passed. `dotnet test`/build are **không chạy được** because NU1900 cannot reach NuGet before compilation; `git diff --check` passed. The prior owner-reported Integration PASS at `eb05190` does not validate this change; integrated acceptance remains pending.
