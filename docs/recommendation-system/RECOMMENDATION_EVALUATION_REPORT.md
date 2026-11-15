# Recommendation Engine Evaluation Report

**Status:** engineering evaluation; synthetic labels only  
**Evaluated code:** current integration worktree based on `b4e62cb` (evaluation test added in this worktree)  
**Run:** `dotnet test tests/ECommerce.Tests/ECommerce.Tests.csproj --no-restore --filter FullyQualifiedName~RecommendationEvaluationTests --logger "console;verbosity=detailed"`  
**Persistence:** isolated EF `InMemory` test database; no persistent database was opened or changed.

## A. Synthetic evaluation

### Dataset and temporal methodology

The executable benchmark is the deterministic fixture embedded in [`RecommendationEvaluationTests.cs`](../../tests/ECommerce.Tests/Unit/RecommendationEvaluationTests.cs). It has 4 synthetic products, 2 brands, one typed numeric attribute, and 5 synthetic customer histories. Each user has one eligible training purchase before the development cutoff, one development-period target purchase, and one later holdout target purchase. Catalog products are available before all cutoffs. Orders, payments, items, and derived purchase signals at or after each exclusive cutoff are excluded by the production service.

The split is temporal, not user-disjoint. The baseline reproduces the previous category/price weights (0.60/0.40) with new feature weights disabled. The Catalog V2 variant uses the current configured weights (category 0.40, price 0.20, brand 0.15, attributes 0.20, characteristics 0.05). Those current weights are implementation defaults, not holdout-tuned parameters.

| Window | Prediction cutoff | Relevance labels |
|---|---|---|
| Training history | Before 2026-01-01 UTC | First purchase per user is visible to the profile |
| Development | 2026-01-01 UTC, exclusive | Product 30 per user, purchased after the cutoff |
| Holdout | 2026-02-01 UTC, exclusive | Product 40 per user, purchased after the cutoff; development purchases are now historical |

Labels are author-generated from the fixture construction and are **synthetic**, not human reviewed. The holdout split was evaluated for reporting only and was not used to change weights or thresholds. The baseline disables brand and attribute signals while keeping category and price weights; the Catalog V2 variant uses the configured category, price, brand, and structured-attribute signals. The identical implementation and cutoffs are used for each comparison.

The supplied V2 data package separately contains `RECOMMENDATION_EVALUATION_SPLIT_V2.csv` with 180 temporal user/cutoff/holdout rows and notes that the benchmark is synthetic. That full 180-row dataset was inspected for its schema and methodology, but **was not executed against the Recommendation Service in this run**. Its results must not be inferred from the small in-memory fixture below.

### Metrics and measured results

Binary relevance is used. For each user, Precision@K is relevant returned items divided by K; Recall@K is relevant returned items divided by all expected relevant items; NDCG@K uses binary gain and logarithmic rank discount, normalized by ideal DCG; MRR@K is the reciprocal rank of the first relevant item, or zero if none appears in the first K. Macro averages give each user equal weight. K is 1 or 3 as shown.

| Scenario / split | Variant | Users / cases | P@1 | P@3 | R@3 | NDCG@3 | MRR@3 |
|---|---|---:|---:|---:|---:|---:|---:|
| Personalized / development | Category + price baseline | 5 | 0.0% | 33.3% | 100.0% | 63.1% | 50.0% |
| Personalized / development | Catalog V2 brand + attributes | 5 | 100.0% | 33.3% | 100.0% | 100.0% | 100.0% |
| Personalized / temporal holdout | Category + price baseline | 5 | 0.0% | 33.3% | 100.0% | 63.1% | 50.0% |
| Personalized / temporal holdout | Catalog V2 brand + attributes | 5 | 100.0% | 33.3% | 100.0% | 100.0% | 100.0% |
| Similar products | Category + price baseline | 1 | 0.0% | 66.7% | 100.0% | 69.3% | 50.0% |
| Similar products | Catalog V2 brand + attributes | 1 | 100.0% | 66.7% | 100.0% | 100.0% | 100.0% |

P@3 remains unchanged in the personalized case because the fixed denominator is three and each case has one target; the measured gain is the relevant item's rank (P@1, NDCG, MRR). In the similar-product fixture there are two expected products, so P@3 is also unchanged while ordering improves.

### Personalized, similar, cold-start, and fallback

- **Personalized:** purchase history contributes distinct-order category and brand signals, typed-attribute preferences weighted by purchased quantity, price affinity, and same-category product-characteristic similarity. In this fixture the enriched service ranks the known matching brand/attribute target first in both temporal windows.
- **Similar product:** the target and candidates use category, brand, comparable typed attributes, text characteristics, and price. The enriched variant ranks both expected neighbors before the decoy.
- **Cold start:** a user with no eligible purchase history reached the existing `popular` fallback and received 2 visible products. This is a fallback-path check, not a relevance-quality score because the fixture has no cold-start user labels.
- **No-sales fallback:** existing unit test `GetForUserAsync_UsesTopRatedFallbackWhenNoEligibleSalesExist` verifies the `top_rated` fallback. It is a path test, not a measured quality result.

### RFM segmentation

The test invokes the production `RfmService` at the holdout cutoff, then reports personalized holdout metrics grouped by the resulting segment. On this five-user fixture the observed groups were:

| Segment | Users | P@3 | R@3 | NDCG@3 |
|---|---:|---:|---:|---:|
| Champions | 2 | 33.3% | 100.0% | 100.0% |
| NeedsAttention | 3 | 33.3% | 100.0% | 100.0% |

These very small groups only demonstrate that metrics can be stratified by the production RFM result. They are not evidence that the segment-specific quality generalizes.

### Analysis and limitations

This fixture verifies that catalog V2 features affect rankings as intended and that the API/RAG share one service. Its perfect top-1 enriched metrics are expected from deliberately constructed labels and must not be presented as real customer quality. The benchmark has only four products, five users, one category, one attribute, and one relevance target per personalized case. It does not establish statistical significance, generalize across categories, or evaluate recommendation satisfaction, returns, clicks, exposure bias, or production conversion.

The available 180-row synthetic temporal dataset still needs an offline execution against a reproducible V2 catalog/order snapshot to produce broader measured metrics. Do not tune on its holdout: choose parameters on training/development only, then report the holdout once and retain a dated record. No human-reviewed Recommendation labels are currently included here.

## B. Human-reviewed evaluation

**No data.** There are no owner-confirmed Recommendation labels in the available evaluation package. Do not combine the synthetic fixture metrics above with human-reviewed results or describe them as validated customer relevance.

## C. Demo regression and integration evidence

The current test suite includes service tests for temporal cutoffs, brand/structured attribute ranking, similar-product ranking, cold-start/popularity and top-rated fallbacks, adapter delegation to the shared `IRecommendationService`, infrastructure DI registration, and Assistant recommendation-intent routing with customer identity preserved. The test suite result recorded for this change is **213 passed, 0 failed, 0 skipped**; the focused deterministic evaluation test also passed.

The test suite is not a live API/database acceptance run. Runtime against `ECommerceDb_Dev`, frontend acceptance, and end-user recommendation relevance on real catalog history remain separate owner acceptance steps.
