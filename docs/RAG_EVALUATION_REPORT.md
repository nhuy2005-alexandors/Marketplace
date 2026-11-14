# RAG evaluation report

**Deduplicated cache evaluation source:** repository/runtime/cache baseline `86fb0dc` (branch `integration/khang-rag-v2-contract`). The report was generated from the checked-in evaluator logic and local embedding caches; no API, Gemini, or database was called. The cache has 1,395 valid unique catalog vectors for 1,500 chunk records (repeated chunk content hashes share vectors), 214 valid query vectors, and no missing vector for the 120-row V2 evaluation file or the evaluated K-05/no-evidence fixtures. Do not confuse the 1,395 cached vectors with the 1,500 chunk records.

## A. Synthetic evaluation

All labels in this section come from synthetic catalog predicates or author-written fixtures; they have not been owner-reviewed. These numbers describe engineering behavior on a generated catalog. They are **not evidence that the RAG system has real-world relevance quality or is ready for release**.

### V2 query normalization and deduplication

Deduplication uses the exact `RetrievalScoring.Normalize` runtime method: lowercase invariant, Unicode accent removal, `đ`→`d`, punctuation-to-space, whitespace collapse, and its existing Vietnamese compound normalization. Expected Product ID sets are compared after sorting and deduplication. A normalized group with different Expected Product ID sets is a hard error; no label is selected automatically. Development/holdout overlap is also a hard error.

| Split | Raw rows | Unique queries | Answerable unique | No-result unique | Duplicate groups |
|---|---:|---:|---:|---:|---:|
| Development | 96 | 88 | 80 | 8 | 8 |
| Holdout | 24 | 21 | 19 | 2 | 3 |
| Total | 120 | 109 | 99 | 10 | 11 |

The independent counts in the task were confirmed: 11 duplicate groups (22 source rows), all under `category_brand_vi` or `mixed_language`; 0 groups with conflicting Expected Product IDs; 0 normalized-query leaks between development and holdout. Duplicate pairs in holdout are `RAG-V2-031/041`, `RAG-V2-036/046`, and `RAG-V2-096/106`. No development duplicate group crosses a type boundary.

| Split / type | Raw rows | Unique queries |
|---|---:|---:|
| Development / category_brand_vi | 16 | 10 |
| Development / exact_sku | 12 | 12 |
| Development / mixed_language | 12 | 10 |
| Development / multi_attribute_vi | 16 | 16 |
| Development / no_result | 8 | 8 |
| Development / product_name | 12 | 12 |
| Development / single_attribute_vi | 20 | 20 |
| Holdout / category_brand_vi | 4 | 2 |
| Holdout / exact_sku | 3 | 3 |
| Holdout / mixed_language | 3 | 2 |
| Holdout / multi_attribute_vi | 4 | 4 |
| Holdout / no_result | 2 | 2 |
| Holdout / product_name | 3 | 3 |
| Holdout / single_attribute_vi | 5 | 5 |

### Formulas

- **Recall@5 (R5):** for each unique Answerable query, `|Expected IDs ∩ first 5 ranked product IDs| / |Expected IDs|`; report the arithmetic mean over unique Answerable queries. Each unique query has equal weight. This is the uncapped `R5(all)` convention used by the previous evaluator.
- **MRR@10:** for each unique Answerable query, reciprocal rank of the first Expected ID in the first 10 ranked product IDs, or zero if none is present; average over unique Answerable queries.
- **No-result false-positive rate:** number of unique No-result queries whose *final user-visible product list* is nonempty divided by unique No-result queries. The offline reproduction applies the current scope guard, then the current ranking and four-product response limit. The V2 no-result rows are nonexistent explicit SKUs, so no mutable SQL filter data is involved.
- **Answerable coverage:** unique Answerable queries with at least one Expected ID in the final four-product visible list divided by unique Answerable queries.
- **Raw / Unique:** raw is the JSONL row count; unique is the count after runtime normalization and label-consistency validation. Fixture metrics below are never pooled across fixture families.

### Deduplicated cache results

Threshold and semantic weight are held at `0.59 / 0.25`; they are not retuned. Semantic results use the cached query vectors and the same `RetrievalScoring` implementation. This is a new **deduplicated** result, next to (not in place of) the historical raw-row results.

| Set | Mode | Raw / unique | Answerable / no-result | R5 | MRR@10 | No-result FP (final visible) | Answerable coverage (top 4) |
|---|---|---:|---:|---:|---:|---:|---:|
| Development | Lexical | 96 / 88 | 80 / 8 | 49.22% | 77.02% | 0.00% | 81.25% |
| Development | Hybrid | 96 / 88 | 80 / 8 | 57.52% | 94.77% | 0.00% | 96.25% |
| Holdout | Lexical | 24 / 21 | 19 / 2 | 51.36% | 84.21% | 0.00% | 89.47% |
| Holdout | Hybrid | 24 / 21 | 19 / 2 | 55.00% | 90.79% | 0.00% | 100.00% |
| K-05 | Lexical | 30 / 30 | 30 / 0 | 0.19% | 1.67% | N/A | 3.33% |
| K-05 | Hybrid | 30 / 30 | 30 / 0 | 4.93% | 18.17% | N/A | 16.67% |
| No-evidence author fixture | Hybrid | 25 / 25 | 0 / 25 | N/A | N/A | 88.00% | N/A |
| Natural-query author fixture | — | 40 / 40 | category labels; SQL filter metadata not cached | N/A | N/A | N/A | N/A |
| Intent author fixture | — | 80 / 80 | intent labels, not product labels | N/A | N/A | N/A | N/A |

The no-evidence fixture's 88.00% is a cache-only diagnostic of the final visible product list after the current scope guard and top-four response limit; it is not a new runtime change. The current rule still has substantial false positives on author-written no-evidence questions. The natural-query fixture includes in-stock and price predicates, but the dry-run cache contains text chunks and vectors, not current SQL stock/price DTOs; those product relevance metrics are therefore N/A instead of being guessed. Intent fixtures measure intent classification, not product retrieval; the cache-only script recorded 80/80 expected intent classifications.

### Historical raw-row results preserved

These prior measurements are retained exactly as recorded. The new calculations do not overwrite them.

| Dataset | Historical results | Context |
|---|---|---|
| Development, raw rows | Lexical R5 46.60% / MRR@10 74.56%; hybrid R5 55.48% / MRR@10 94.68% | The `0.59 / 0.25` selection was made on development at `3db14e5`; these are in-sample figures. |
| Holdout, one run at `3db14e5` | Lexical R5(all) 48.80%, MRR@10 86.36%; hybrid R5(all) 52.45%, MRR@10 92.05% | Previously exposed; no longer a pristine holdout. Threshold was selected from development at `3db14e5`. |
| K-05, reporting-only at `3db14e5` | Hybrid MRR@10 18.17% | 30 author-written rows; reporting only, not used for tuning. |

The holdout has been inspected and must not be described as absolutely clean. It has only 19 unique answerable queries after deduplication, and one query materially moves the average. Labels are predicate-derived and not human-reviewed. Treat the holdout numbers as engineering sanity checks, not a quality claim. Any future change to ranking invalidates comparisons against these historical holdout figures; retain the old numbers side by side.

### Owner-reviewed set

`RAG_OWNER_REVIEWED_QUERIES.jsonl` has 12 draft rows, all `owner_confirmed=false` and with empty expected product IDs. **Human-reviewed evaluation: chưa có dữ liệu.** It is kept out of every synthetic metric above. Run `powershell -NoProfile -File .\tools\rag\evaluate-v2.ps1 -OwnerReviewedOnly -DryRunFromCache -DryRunDataPath .\obj\rag-dryrun-data.json` to see the explicit no-data result. Once the owner marks rows confirmed and supplies labels, the mode deduplicates those rows, checks label conflicts, and reports them separately.

Before confirming a draft, the owner can run:

```powershell
powershell -NoProfile -File .\scripts\local-acceptance\owner-query-candidates.ps1
```

This read-only helper restricts itself to `ECommerceDb_Dev`, sets SQL `ApplicationIntent=ReadOnly`, validates the connected database, and issues SELECT statements. It lists catalog candidates; the owner must decide the expected IDs and whether a no-result label is correct. For the existing V2 file, `label-check.ps1` checks ID existence and available lexical evidence. The source fixture contains no explicit `label_predicate` field: except exact SKU and no-result checks, its lexical result is only a diagnostic and **cannot certify semantic/category/attribute label correctness**. Human review remains necessary.

## B. Human-reviewed evaluation

No owner-confirmed query rows are currently available. Report status: **chưa có dữ liệu**. Do not combine this empty section with synthetic metrics. Twelve draft scenarios cover natural semantic wording, brand, RAM, battery capacity, screen size, ANC, price plus category, and expected no-result cases for a nonexistent condition/brand and an out-of-scope question.

## C. Demo regression tests

These are implementation regressions, not retrieval quality metrics:

- The existing golden retrieval fingerprint remains the ranking guard; it was not edited. Run `powershell -NoProfile -File .\tools\rag\tests\Test-RetrievalScoringGolden.ps1` to verify the existing expected fingerprint.
- `Test-DeduplicatedEvaluation.ps1` covers case/accent/whitespace normalization, conflicting labels, cross-split leakage, metric formulas, and empty/fake owner-confirmed mode.
- Owner-reported guest smoke at `eb05190`: PASS=16, FAIL=0, KNOWN_LIMITATION=1; owner also reported DB counts unchanged. This is demo regression evidence and is not included in synthetic evaluation metrics.
- Owner-reported at `eb05190`: `dotnet test` Passed 129 / Failed 0 / Skipped 0. These results are not rerun claims for this documentation/evaluation update.
