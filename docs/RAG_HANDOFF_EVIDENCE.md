# RAG handoff evidence

**Branch:** `integration/khang-rag-v2-contract`  
**Evidence checkpoint:** owner report for `7f01c18`; handoff document commit follows on the same branch.
**Status values below:** owner-reported results are identified as such; this document does not claim they were rerun in the current sandbox.

## Acceptance summary

| Area | Current status | Evidence / limitation |
|---|---|---|
| Integration | **PASS (owner-reported at `7f01c18`)** | Owner reports the API build and `dotnet test` passed **128/128**; no failures/skips were reported (128/0/0). The GenerationEnabled default-off change after that checkpoint has not yet had a .NET run in this sandbox. |
| Frontend | **PASS** | Owner reports frontend build and lint passed. Exact commands/log artifacts: **chưa kiểm chứng**. |
| Localhost | **PASS một phần** | Guest/no-auth path was exercised; there has not been acceptance with an authenticated account. Owner reports the previous smoke 15/16: the strict-empty assertion fails because Direction D keeps nearby suggestions. The updated smoke reports this case as `KNOWN_LIMITATION` and separately checks weak-evidence semantics; owner rerun is pending. |
| Database safety | **PASS theo báo cáo chủ sở hữu** | Owner reports counts unchanged across multiple API runs and API logs contained SELECT only. No database was accessed while preparing this handoff. Snapshot artifacts and exact before/after counts: **chưa kiểm chứng**. |
| Manual UI | **Owner-reported PASS** | Owner reports manual interface testing looked good. No screenshots or reproducible test record were supplied; detailed visual evidence is **chưa kiểm chứng**. Follow [MANUAL_FRONTEND_TEST_CHECKLIST.md](MANUAL_FRONTEND_TEST_CHECKLIST.md). |
| Integrated AI | **CHƯA PASS** | Requires Khánh to merge the shared Recommendation contract and the owner to complete integrated acceptance. |

## Retrieval and evaluation evidence

Metrics below are those recorded in `RAG_IMPLEMENTATION_LOG.md`; they are offline/cache-based measurements on the synthetic Marketplace V2 catalog, not live production quality guarantees.

| Dataset / mode | Recorded result | Notes |
|---|---|---|
| Development, lexical | Recall@5 46.60%; MRR@10 74.56% | 88 queries with expected results; 8 no-result queries evaluated separately. |
| Development, hybrid | Recall@5 55.48%; MRR@10 94.68% | Selected on development at semantic threshold 0.59 and semantic weight 0.25. |
| Development, no-result | 8/8 empty in the earlier lexical run | This does not resolve the live false-positive issue for general nonsense/unrelated wording (K-11). |
| Holdout | **Chưa kiểm chứng: no holdout metrics recorded** | A golden ranking fingerprint includes holdout rows for regression only; it is not a holdout metric run. Do not describe it as a holdout PASS. |
| K-05 author paraphrases | R5(all): 13.3-inch 5.56%, 512 GB 2.78%, Bluetooth 6.47%; hybrid R5(capped): 20%, 10%, 22% respectively | At threshold 0.59 / weight 0.25. K-05 is author-written and not human-reviewed; these results are diagnostic only and were not used for tuning. |
| No-evidence vs natural-query feature report | Best measured candidate gap: no-evidence weak 48%, natural weak 2.5%; no perfect split | Cache-only feature diagnostics. No candidate rule was selected or applied. Absolute scores depend on the embedding model and synthetic catalog. |

The development/holdout query fixture has 96 rows (88 relevant, 8 no-result); K-05 has 30 author-written queries. The log also records a 175-query golden scoring fingerprint, which guards ranking output but does not calculate holdout metrics.

Owner reports **11 migrations**, no new migration in this work, no tracked secret files, and `.env.example` unchanged from baseline. Migration-history output and a tracked-file audit were not independently captured in this handoff: **chưa kiểm chứng**.

## Known limits and risks

- **K-11 is reduced nhẹ and remains open.** The recorded feature report classified 18/25 no-evidence queries as weak. Owner reports 4/8 known smoke queries were labeled weak. These labels are not the same as strict-empty retrieval correctness: Direction D intentionally retains nearby suggestions. Unrelated/no-evidence wording has produced false-positive products in live testing; neutral response wording does not fix retrieval selection. The updated smoke separates the weak-response contract from a strict-empty case reported as `KNOWN_LIMITATION`.
- **Generation is not confirmed working.** Owner reported no successful generated answer at the latest probe: timeouts, HTTP 429s, and circuit-breaker fallback were observed. Safe quota logging is now available, but the quota cause remains unconfirmed.
- **K-05 is weak, especially for structured numeric attributes.** Correct and incorrect products have close semantic maxima; do not treat K-05 as a tuned benchmark.
- **Weak-evidence wording and author-created evaluation labels are not human-approved.** K-05, no-evidence, and natural-query fixtures need review before being treated as acceptance truth.
- **Holdout is small (24 queries) and has no recorded metric result.** Keep it isolated. Any future behavior change after inspecting it means it is no longer a clean holdout; report it beside the unchanged baseline and label that limitation.
- **Gemini key/quota and data terms require owner review.** Current evidence shows quota/timeout constraints. Confirm the selected plan's quota, retention, and data-use terms before sending catalog or user content; keep credentials server-side and rotate any key that was exposed outside the secret store.
- **Manual UI evidence is incomplete.** The owner reported the UI looked good, but there are no screenshots or structured acceptance notes.
- **Authenticated localhost flow is not covered by the reported guest smoke.** Login/session behavior and role-specific access remain **chưa kiểm chứng** in this handoff.

## Rollback

1. Set server configuration `Gemini:RetrievalSemanticThreshold=0.38` and `Gemini:RetrievalSemanticWeight=0.5` to restore the prior retrieval configuration. Keep these as configuration values; do not edit ranking code to roll back.
2. Set `Gemini:GenerationEnabled=false` to disable only Gemini answer generation; strong-evidence responses use the local grounded answer with `answerSource="fallback"`. Embedding and retrieval remain enabled. This switch is server-side configuration and defaults to false. Set it to true only after separate owner approval and provider acceptance.
3. To revert a deployed implementation change in a shared checkout, confirm the branch and clean working tree, then use `git revert <implementation-commit>` for the specific behavior/configuration commit. This creates an inverse commit and does not rewrite history. Review the diff and rerun applicable tests; do not use `git reset --hard` on a shared branch.
4. For a full rollback to before the RAG lane, revert all dependent implementation commits newest-to-oldest and have the owner review the combined diff. `7f01c18` is the earlier implementation checkpoint; it is not automatically the right single commit to revert for a targeted feature rollback. Retain this evidence document unless it also needs to be removed.

## Owner acceptance steps

1. Confirm the working branch/commit and keep the Gemini key in the server secret store; do not paste it into logs or reports.
2. Verify `ECommerceDb_Dev` identity and record read-only before/after counts and migration history. Start the API only after confirming its migration behavior is safe; verify API logs remain SELECT-only for the smoke scenarios.
3. Run the updated guest smoke against localhost. Record each HTTP status, answer source, evidence label, retry marker, product/source IDs, and final summary. The 413 case should pass on HTTP 413 with a safe error body and must not require `answerSource`; record whether other cases were retried after 429.
4. Run the authenticated user flow separately and capture reproducible notes or screenshots for the manual UI check. Record browser/device, account role, relevant query/filter scenarios, and any failures.
5. Ask Khánh to merge the shared Recommendation contract into the agreed integration commit. Confirm both lanes use the same commit before declaring Integrated AI PASS.
6. Generation is disabled by default. If owner-authorized to probe, run `powershell -NoProfile -File .\scripts\local-acceptance\gemini-generation-probe.ps1 -Models 'model-a,model-b' -Thinking none` (or `low`/`medium` as explicitly selected), review `models.get` capabilities and safe quota fields, and leave pacing enabled. Do not put a key on the command line. Re-enable generation only after the owner approves quota/data terms and a generated response is verified.
7. Complete owner review of weak-evidence wording and author-written labels. Keep K-05 reporting-only; do not tune on holdout.
8. Run the agreed integrated acceptance suite on the final shared commit. Only after guest and authenticated flows, DB safety, provider behavior, and shared contract all pass should the owner change the status to **Integrated AI PASS**.

## Evidence provenance

- Owner-reported at checkpoint `7f01c18`: build OK, `dotnet test` Passed 128/128 (128/0/0), frontend build/lint passed, 11 migrations with no new migration, no tracked secrets, `.env.example` unchanged, API database counts unchanged over multiple runs with SELECT-only logs, manual UI looked good, and guest smoke 15/16 with the strict-empty case expected under Direction D; updated smoke rerun is pending.
- Repository-recorded offline measurements: [RAG implementation log](RAG_IMPLEMENTATION_LOG.md), [assistant API delta](OPENAPI_ASSISTANT_DELTA.md), and the fixtures under `docs/rag-evaluation/`.
- Not independently verified in the sandbox for this handoff: owner machine test/build output, frontend build output, database snapshots/logs, screenshots, live Gemini generation success, a holdout metric run, and authenticated localhost acceptance.
- The generation-off code/configuration and its new C# unit test are newer than the owner-reported `7f01c18` checkpoint; validate them with the owner's .NET build/test before relying on the updated integration status.
