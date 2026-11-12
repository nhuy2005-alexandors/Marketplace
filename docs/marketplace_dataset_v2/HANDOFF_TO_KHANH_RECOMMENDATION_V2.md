# Marketplace — Khánh Recommendation / Personalization Handoff (Dataset V2)

**Document status:** IMPLEMENTATION HANDOFF  
**Prepared:** 2026-11-12  
**Project Owner:** Product & Database owner  
**Lane owner:** Khánh — Recommendation / Personalization / Customer Classification  
**Repository:** https://github.com/nhuy2005-alexandors/Marketplace  
**Working branch:** khanh/recommendation-v2  
**Branch base:** khang/ai_rag @ df8bd2fc8f935884eaab9e4d3ca4a2d427956439  
**Related lane:** Khang — RAG / Semantic Search / AI Shopping Assistant (GitHub Issue #1)

---

## 0. Executive assignment

Khánh owns **Lane B: Recommendation / Personalization / Customer Classification**.

The goal is to replace the current client-side recommendation prototype with one backend-authoritative recommendation capability that:

1. Recommends products using valid customer purchase and engagement signals.
2. Generates similar-product results without arbitrary zero-score matches.
3. Handles guest / new / no-history customers honestly.
4. Exposes one reusable backend recommendation service that the Marketplace API and Khang's RAG/Shopping Assistant can both call.
5. Provides a read-only RFM customer classification capability for Admin.
6. Prevents one account's recommendation/search/view state or cached API data from leaking into another account.
7. Runs against the existing Marketplace application and the owner's existing V2 development database after the owner-controlled integration gate.

This is a localhost course-project product, not a production recommender platform. Prefer a deterministic, explainable and maintainable implementation. Do not build a second platform, separate data pipeline, or speculative ML infrastructure.

**Implementation approach:** inspect the current code, then fix the actual implementation directly. Do not spend time creating another mock application, mock database, synthetic dataset, or third database. The V2 dataset is already prepared. Pure deterministic unit tests are still expected; SQL/localhost integration uses the owner's existing V2 database only after explicit authorization.

## 1. Ownership boundaries

### Khánh owns

- Backend recommendation domain/service and ranking logic.
- Personalized recommendations for authenticated customers.
- Similar-product / item-to-item recommendations.
- Cold-start and no-history fallback behavior.
- Read-only RFM customer classification for Admin.
- Recommendation-specific frontend integration: Home page recommendation block, product-detail similar-products block, recommendation store and related API hooks.
- Recommendation reason codes and deterministic recommendation tests.
- Recommendation API DTOs and service contract consumed by Khang.

### Khang owns

- RAG, semantic/hybrid product retrieval, AI Shopping Assistant and ChatBot.
- client/src/components/ChatBot.tsx.
- client/src/services/ragService.ts.
- src/ECommerce.API/Controllers/AssistantController.cs and assistant-specific behavior.

### Shared boundary

The current RecommendationSection dispatches the browser event minishop:ask-bot to open ChatBot with a product-specific question. Preserve this behavior unless Khang agrees to a revised event contract.

Khang must consume Khánh's backend recommendation service for recommendation intents. Khang must not create a second recommendation scoring implementation inside RAG/ChatBot.

Do not edit ChatBot/RAG files directly. If a shared DTO, service interface, event, or DI integration must change, coordinate it through the shared contract and record the agreed interface in the PR.

## 2. Source baseline and branch

### Verified repository state at handoff

- Default branch: main @ ab571d20ae738d0d8cae0b3feceaced508c4013a.
- Khang branch: khang/ai_rag @ df8bd2fc8f935884eaab9e4d3ca4a2d427956439.
- Khang branch is 3 commits ahead of main.
- No Khánh branch existed in the observed remote branch list.
- The dedicated branch khanh/recommendation-v2 has now been created from the exact Khang branch SHA above. It is the working branch for this lane.
- This branch includes Khang's existing three commits. Do not reset it to main, overwrite khang/ai_rag, or force-push.

### First commands

Run from the repository root:

~~~powershell
git fetch origin
git switch --track origin/khanh/recommendation-v2
git status --short --branch
git rev-parse HEAD
git log -1 --oneline
~~~

If a local branch already exists, inspect it rather than replacing it. If the worktree is dirty, preserve and report the changes; do not run git reset --hard, git clean -fd, or discard uncommitted work.

### Pull request target

The initial PR should target khang/ai_rag, not main, because the Khánh branch starts from Khang's three-commit AI baseline. This avoids accidentally bundling Khang's commits as part of a Khánh-to-main PR. Final merge to main remains owner-controlled after both lanes are integrated.

If Khang's branch advances while Khánh is working, coordinate before rebasing/merging. Do not force-push.

## 3. Existing implementation — verified source findings

The following are findings from the remote khang/ai_rag source at the pinned baseline, not assumptions about a local worktree.

### 3.1 Recommendation currently runs in the browser

Relevant files:

- client/src/pages/HomePage.tsx
- client/src/services/recommendationService.ts
- client/src/store/recommendation.ts
- client/src/components/home/RecommendationSection.tsx
- client/src/pages/ProductDetailPage.tsx

Observed behavior and defects:

1. HomePage.tsx requests only 20 products with sortBy price descending and passes that page to the recommendation functions. The candidate pool is therefore the 20 most expensive products, not the complete eligible catalog.
2. The browser loads useOrders(1, 20), cart and wishlist, then constructs the user profile and scores products locally. The profile is based on at most the first 20 returned orders.
3. buildUserProfileFromData currently adds every order item without checking parent order status, payment status, or cancelled item fulfillment status. Pending/cancelled orders can therefore become purchase signals.
4. computePersonalizedRecommendations uses fixed client-side weights for purchased, wishlist, cart, viewed, category and search signals. The generated per-product reasons are not returned in the final result; the UI receives only one generic reason string.
5. scoreSearchSemantic is lexical substring/token/synonym matching. It is not an embedding-based semantic recommender. Do not describe it as a trained or neural model.
6. computeSimilarProducts uses category, same seller, price band and name-token overlap. It sorts and returns the top items even when similarity is zero; it has no minimum relevance threshold and does not use brand or structured product attributes.
7. computeTrendingRecommendations sorts by rating and review count. This is a top-rated fallback, not a sales-trending model. The UI's AI Match / trending labels can overstate what the implementation actually measures.
8. client/src/store/recommendation.ts persists all users' recommendation state under one fixed localStorage key: minishop-recommendations. Search history, viewed product IDs and active recommendations are not partitioned by account.
9. The React Query hooks for orders, cart and wishlist also use account-agnostic keys. Inspect account transitions and ensure one user's cached data cannot be rendered for another user after logout/login.
10. There is no backend recommendation controller/service or recommendation-specific test suite in the pinned source tree. The Application DI currently registers commerce services but no recommendation service.

### 3.2 Relevant existing backend contracts

- src/ECommerce.Domain/Entities/Order.cs: parent order status and order items.
- src/ECommerce.Domain/Entities/OrderItem.cs: product ID, seller ID, quantity, unit price and item-level fulfillment status.
- src/ECommerce.Domain/Entities/Payment.cs: amount, payment status, paid timestamp.
- src/ECommerce.Domain/Entities/Review.cs: user, product, rating and created timestamp.
- src/ECommerce.Domain/Entities/Cart.cs, CartItem.cs, WishlistItem.cs: current engagement state.
- src/ECommerce.Domain/Entities/Product.cs: category, seller, price, stock, description and image URL in the pinned remote model.
- src/ECommerce.Application/Interfaces/IAppDbContext.cs and src/ECommerce.Infrastructure/Persistence/AppDbContext.cs: current EF query surface.
- src/ECommerce.Application/Services/OrderService.cs: checkout/cancellation behavior.
- src/ECommerce.Application/Services/PaymentService.cs: payment completion behavior.
- src/ECommerce.Application/Services/ReviewService.cs: review eligibility behavior.
- src/ECommerce.Application/Services/DashboardService.cs: existing dashboard aggregation; do not blindly reuse its paid-order predicate for recommendation/RFM because it filters by parent order status but does not require Payment.Status == Completed.
- src/ECommerce.API/Controllers/AdminController.cs: existing Admin-only controller surface.
- src/ECommerce.API/Services/CurrentUser.cs: authenticated user identity abstraction.
- src/ECommerce.Application/DependencyInjection.cs: Application service registration.
- src/ECommerce.API/Program.cs: startup behavior and middleware.

Important: ReviewService.CreateAsync currently checks that an order is not Cancelled, but does not require a completed payment or delivered fulfillment. Do not treat every review or non-cancelled order as proof of an eligible purchase.

## 4. Dataset V2 — authoritative development data contract

### 4.1 Shared dataset location

[Marketplace Dataset V2 — Google Drive](https://drive.google.com/drive/folders/1aCVvSGGsMatv3pmAnY-IM4OGT1VzfMrs)

V2 fingerprint:

d1c1b4626f38dedce51cee04fdaae0dabe37782c92dc23c9a13cf5c29f745700

The owner reports the V2 replacement and post-import SQL validation succeeded on the existing ECommerceDb_Dev. The V2 backup passed RESTORE VERIFYONLY WITH CHECKSUM; a full restore test has not been performed.

### 4.2 V2 population

| Entity / metric | Count |
|---|---:|
| Products | 500 |
| Categories | 8 |
| Brands | 35 |
| AttributeDefinitions | 48 |
| ProductAttributeValues | 3,000 |
| Customers | 1,000 |
| Sellers | 20 |
| Orders | 5,000 |
| OrderItems | 12,500 |
| Payments | 4,700 |
| Reviews | 1,800 |
| Carts | 200 |
| CartItems | 200 |
| WishlistItems | 1,250 |
| Coupons | 8 |
| Multi-seller orders | 1,500 / 5,000 (30%) |

Customer eligible-order cohorts:

- 150 customers with zero eligible purchases.
- 150 customers with one eligible purchase.
- 350 customers with four eligible purchases.
- 250 customers with seven eligible purchases.
- 100 customers with eleven eligible purchases.

The generated preference patterns include stable category, brand and price affinities expressed through repeated purchase behavior, with deliberate exploration. Latent profile labels are not stored in application tables and must not be used as model inputs.

### 4.3 Synthetic-data limitations

- All customers, sellers, products, brands, reviews, payments and transactions are fictional demo records.
- USD is the synthetic domain convention. Do not silently convert to VND or mix currencies.
- Product image coverage is 0/500.
- No manufacturer-backed product facts or real-world user behavior are present.
- The dataset supports localhost integration and controlled engineering evaluation; it does not establish production or real-world recommendation quality.
- Do not generate another dataset or alter the shared V2 package.

### 4.4 Eligible purchase semantics — mandatory

For recommendation purchase history, customer preference features, RFM Frequency and RFM Monetary:

- Eligible parent order statuses: Paid, Shipped, Delivered.
- Payment must have Payment.Status == Completed.
- Exclude Pending and Cancelled orders.
- At item level, exclude OrderItem.Status == Cancelled from product/category/brand/attribute purchase signals, even if the parent order itself is eligible.
- Seller fulfillment is item-scoped. Do not infer every item's state from Order.Status.
- Use distinct Order.Id for Frequency; do not count order items as separate orders.
- Use net completed payment/order amount after coupon discount for Monetary, with one amount counted once per order. Reconcile Payment.Amount against the order total; do not sum the order total once per item.

The V2 mapping states eligible purchases require paid/shipped/delivered parent order status plus a completed internal mock payment. Pending/cancelled orders are not purchases. The V2 generator also ensures cancelled orders restore stock and have no payment.

### 4.5 Identity and temporal evaluation contract

Files:

- [Recommendation evaluation split CSV](https://drive.google.com/file/d/1y9SIdvx1L-HyEiyzyaNo4WlJvuG8nx_5/view)
- [Recommendation evaluation notes](https://drive.google.com/file/d/18oPVCsK5YqUVZOPFpDo5rCe8oeS5DeR_/view)
- [Customer transaction mapping](https://drive.google.com/file/d/168_QLBLTCAvp2zS1up0dMeIp7LeF2s82/view)

Rules:

1. UserId is the actual Users.Id FK. CustomerOrdinal is a generator sequence number, not a database key. Never join on CustomerOrdinal.
2. The split contains 180 distinct users: 60 from each of the 4-, 7- and 11-eligible-order cohorts.
3. The latest eligible order is held out. TrainingEligibleOrderCount is 3, 6 or 10 respectively.
4. CutoffExclusiveUtc is ISO-8601 UTC with a trailing Z. SQL Server datetime2 stores UTC-naive values by explicit dataset convention.
5. Only eligible Orders with CreatedAt < CutoffExclusiveUtc are training history. Strictly earlier means <, never <=.
6. Apply the same exclusive cutoff to every interaction source and every derived feature: Orders, OrderItems, Reviews, Carts, CartItems, WishlistItems and derived category/brand/price aggregates.
7. The holdout target is the product set in the single latest eligible order at the cutoff. Use HoldoutProductIds as targets, not training features.
8. Keep the provided holdout target set isolated from ranking/model tuning. The split contains one latest-order holdout per evaluation user; do not fit weights to those target ProductIds. If a separate development protocol is needed, document it and get owner approval first.
9. The dataset's 15% exploration means purchase history is not a perfect expression of preference. Avoid treating every purchase as an unambiguous long-term affinity.

**Evaluation warning:** the split's expected targets are generated from deterministic synthetic purchase histories. They are useful for controlled offline checks but are not human-judged relevance labels or evidence of real customer preference.

## 5. P0 dependency: V2 database schema is ahead of remote source

This is a verified integration gap and must not be hidden.

- The V2 LocalDB execution/validation reports state that the existing ECommerceDb_Dev has 11 applied EF migrations and includes Brands, AttributeDefinitions and ProductAttributeValues.
- The pinned remote source at df8bd2fc8f935884eaab9e4d3ca4a2d427956439 contains only the seven original migration files. Its AppDbContext, IAppDbContext, Product model and model snapshot do not expose the V2 Brand / AttributeDefinition / ProductAttributeValue model.
- Therefore the remote code currently available to Khánh does not fully represent the schema in the owner's V2 LocalDB. A direct API run from this source could attempt migration/seeding against a schema it does not model.

**Required behavior:**

1. Do not create or invent migrations to close this gap.
2. Do not connect to the owner's LocalDB or launch the API against it.
3. Report this as BLOCKED — V2 schema source is not yet published for any feature requiring Brand or structured ProductAttributeValue access.
4. Continue work that can be completed against the already-published entities without those fields, but keep brand/attribute-based ranking explicitly pending.
5. The Project Owner / Terra must publish the exact V2 EF entities, DbSets, configurations, migrations and model snapshot to the shared repository before Khánh can compile/query those fields or run full integration.
6. Once the owner publishes that source, rebase/merge the exact approved commit and run a targeted schema/model alignment check before DB integration.

This is not permission to change the database schema. The database is already prepared; the missing deliverable is the matching source representation.

## 6. Target architecture and interface

### 6.1 Backend is authoritative

Move candidate generation, purchase-signal filtering, ranking, availability filtering and reason generation to the ASP.NET Core backend.

- Derive the authenticated user ID from the JWT / ICurrentUser. Never trust a client-supplied userId for personalized recommendations.
- Client may send a bounded request such as limit, product ID for similar-items, or explicit non-sensitive context; it must not send the authoritative candidate ProductId list.
- Query the eligible catalog from SQL, not from the browser's current page of 20 products.
- Return backend Product DTOs and per-item reason codes.
- Price and stock must be read from the current database/catalog response. Do not rely on stale values copied into a recommendation cache or embedding.

### 6.2 One service shared by API and RAG

Implement one Application-level service/interface (name can follow repository conventions, e.g. IRecommendationService) and register it through the existing Application DI path.

It should support, at minimum:

- Personalized recommendations for the authenticated customer.
- Similar products for a target product.
- A reusable recommendation operation that Khang's AssistantController can call for recommendation intents.

Do not put scoring inside controllers, React components, ChatBot or RAG code. Keep the ranking logic in one backend service.

Suggested DTO shape (adapt to existing conventions; do not treat these names as an immutable external API):

- Recommendation response: items, reason/strategy label, generated-at timestamp, fallback status.
- Recommendation item: Product DTO, rank/score if useful, stable reason-code list.
- Reason code: machine-readable code plus safe display label.

Reason codes should be factual and explainable, e.g. category affinity, brand affinity, attribute match, price-band match, cart/wishlist signal, recently viewed, or popular/top-rated fallback. Do not emit a reason unless the corresponding signal actually contributed to the result.

### 6.3 Candidate generation and ranking

Minimum behavior:

1. Candidate pool is drawn from the full eligible catalog.
2. Exclude the target product from similar-product results.
3. Respect stock/visibility rules used by the existing catalog and checkout. At minimum, do not recommend out-of-stock products.
4. Use valid purchase history and current engagement signals.
5. Use structured category/brand/attribute information when the owner-published V2 EF source becomes available.
6. Use price similarity in USD with a documented relative or band-based calculation, not a hard-coded VND assumption.
7. Add deterministic tie-breaking (e.g. score then ProductId) so identical inputs produce stable output.
8. Do not return arbitrary zero-score candidates. Apply a meaningful minimum relevance threshold and define an honest fallback when no similar item qualifies.
9. Avoid duplicate ProductIds and apply a reasonable diversity rule so one seller/category does not dominate every result unless the evidence supports it.
10. Keep weights and thresholds explicit/configurable/documented. Prefer explainable heuristic ranking over a speculative trained model.

Do not blanket-exclude all previously purchased products without an explicit repeat-purchase policy. The current schema has no replenishable/consumable flag. If a suppression rule is retained, make its behavior explicit and testable.

### 6.4 Cold-start and fallback

Handle these cases separately:

- Guest / unauthenticated visitor.
- Authenticated customer with zero eligible purchases.
- Customer with only one eligible purchase.
- Customer with enough history for personalized ranking.
- Empty or invalid candidate catalog.

Use an honest fallback:
- If ranking is based on ratings/review count, label it top_rated or equivalent.
- If ranking uses eligible paid purchase counts, label it popular or equivalent.
- Do not call either trending unless a time-windowed trend metric is actually computed.
- Do not call it collaborative unless an actual collaborative-filtering algorithm is implemented and evaluated.
- Do not label a generic fallback as AI-personalized.

## 7. RFM Customer Classification — read-only

Implement a read-only Admin capability using the same eligible purchase semantics.

### Required metrics

For each Customer user:

- **Recency:** days since the latest eligible order's CreatedAt, measured against one fixed asOfUtc for the entire request.
- **Frequency:** count of distinct eligible Orders.
- **Monetary:** sum of the net completed payment/order amount after discounts, counted once per eligible order and denominated in synthetic USD.
- Include the last eligible purchase timestamp and a stable segment code.

For zero-purchase customers, return an explicit NoPurchase / New classification. Do not assign artificial RFM quintile scores as if a customer had purchase history.

### Scoring and segment behavior

- Use transparent, deterministic R/F/M scoring. A quintile/rank-based 1–5 score among customers with eligible purchases is a reasonable baseline.
- Lower RecencyDays is better; higher Frequency and Monetary are better.
- Handle ties deterministically using UserId as a secondary sort key.
- Keep segment mapping in one documented function/configuration. Suggested readable groups: Champions, Loyal, PotentialLoyalist, NewCustomer, AtRisk, Hibernating, NeedAttention, NoPurchase.
- Treat exact segment thresholds as an implementation proposal and document them. Do not present them as business facts or persist them to the database.
- Do not add classification tables, columns, stored procedures or migrations. Compute from existing rows at request time or use a bounded in-memory result for the request.

### API/security

- Admin-only endpoint under the existing /api/admin surface (for example, a paginated customer-classification GET endpoint).
- Reuse existing [Authorize(Roles = "Admin")] boundary.
- Return aggregate segment counts plus paginated details if needed.
- Avoid returning email, full name, shipping address, or other PII unless the UI has an explicit owner-approved need. UserId and RFM metrics are sufficient for this course-project view.
- No mutation endpoint; classification is read-only.

## 8. Account isolation and frontend integration

### Required isolation

- Partition recommendation search/view history and active recommendation state by authenticated UserId, or clear all user-scoped state atomically when identity changes.
- Do not reuse one account's persisted search/view/recommendation state after another account logs in.
- Scope React Query keys for user-specific orders/cart/wishlist/recommendation data by authenticated UserId, or ensure a complete and race-safe cache clear on logout/account switch.
- Do not accept a UserId from the browser as authority. The backend derives identity from JWT.
- Test account A → logout → account B and account B → logout → account A. No stale recommendations, carts, orders or wishlist data may cross accounts.

### UI surfaces

- Home: use backend personalized endpoint; remove the local top-20 price-sorted recommendation computation as the authoritative result.
- Product detail: use backend similar-products endpoint.
- Recommendation cards: display the returned factual reason codes/labels, not invented generic explanations.
- Cold-start: display a truthful popular/top-rated/new customer state.
- Keep current page layout and ProductCard usage; avoid broad UI redesign.
- Preserve the minishop:ask-bot event contract for Khang unless both lane owners agree to change it.
- Do not modify ChatBot.tsx or ragService.ts.

## 9. Implementation sequence

### Phase A — read-only source and schema preflight

- Confirm branch, HEAD and worktree status.
- Inspect existing recommendation call sites, API DTOs, authentication identity and DI.
- Record current 7-migration source vs 11-migration V2 DB mismatch.
- Identify exactly which recommendation functions/files are client-only.
- Do not launch the API or touch the LocalDB.
- Produce a concise preflight note in the issue if the V2 schema source is still missing.

### Phase B — backend recommendation service

- Add Application interface and DTOs.
- Implement eligible purchase query semantics.
- Implement candidate retrieval from the full catalog.
- Implement personalized ranking, similar-product ranking, cold-start and factual reason codes.
- Register the service through existing DI.
- Add authenticated API endpoints; derive user identity server-side.
- Keep the service callable from AssistantController without Khang duplicating the scoring logic.

### Phase C — RFM classification

- Implement RFM query and deterministic scoring.
- Implement explicit no-purchase handling.
- Add Admin-only read endpoint and compact admin UI if the existing dashboard structure supports it.
- Do not persist segment labels.

### Phase D — frontend integration and isolation

- Replace HomePage's local recommendation calculation with API data.
- Replace product-detail local similar scoring with API data.
- Partition/reset recommendation state by account.
- Scope user-specific query caches.
- Preserve ChatBot handoff event behavior.

### Phase E — verification and owner integration

- Run build and focused tests.
- Verify API authorization and account isolation.
- Owner coordinates the exact V2 source/migration sync and LocalDB integration window.
- Run SQL/localhost integration only after explicit owner approval and schema-source alignment.
- Capture exact commands, environment, results and commit SHA.

## 10. Acceptance matrix

| ID | Acceptance condition | Required evidence |
|---|---|---|
| REC-01 | Candidate pool is not limited to 20 most-expensive products | Backend query/source inspection + integration evidence |
| REC-02 | Pending/Cancelled orders do not contribute to purchase history | Deterministic tests |
| REC-03 | Payment must be Completed for purchase eligibility | Deterministic tests |
| REC-04 | Cancelled OrderItems do not contribute to product preference | Deterministic tests |
| REC-05 | Frequency counts distinct eligible orders, not items | Unit/SQL evidence |
| REC-06 | Similar-products excludes target and zero-score arbitrary items | Deterministic tests |
| REC-07 | Cold-start labels match the actual fallback signal | DTO/UI tests |
| REC-08 | Each recommendation item has factual reason codes | API response + UI evidence |
| REC-09 | User identity is derived from JWT; client cannot impersonate another UserId | Authorization tests |
| REC-10 | Recommendation store and user caches are account-isolated | A/B account-switch test |
| REC-11 | RFM metrics use eligible order/payment semantics | SQL/unit evidence |
| REC-12 | Zero-purchase customers receive explicit NoPurchase/New classification | Tests + sample response |
| REC-13 | RFM endpoint is Admin-only and read-only | 401/403 + no-write evidence |
| REC-14 | V2 brand/attribute usage is backed by published EF source | Source/schema alignment receipt |
| REC-15 | One backend recommendation service is reusable by API and RAG | DI/interface evidence + Khang integration contract |
| REC-16 | No schema migration/database mutation is introduced by Khánh | Diff review + owner confirmation |
| REC-17 | Existing Marketplace UI and checkout/order flows remain functional | Focused regression evidence |

Do not claim all acceptance criteria PASS if the V2 schema source-sync dependency is unresolved. REC-14 and full LocalDB integration remain BLOCKED until the owner publishes the matching EF source.

## 11. Hard constraints / non-goals

- No DB schema changes or EF migrations by Khánh.
- No new database, per-agent database, clone, restore, reseed or third database.
- Do not touch the original ECommerceDb.
- Do not run the V1-to-V2 importer.
- Do not start the API against the owner's LocalDB. The current Program.cs invokes DbSeeder.SeedAsync during startup, and the README documents automatic migrate + seed behavior. Owner must authorize a safe runtime configuration first.
- Do not assume the owner's LocalDB is remotely reachable from Khánh's machine.
- Do not change checkout, payment, order state machine, ReviewService semantics or stock behavior.
- Do not edit ChatBot/RAG files owned by Khang.
- No mandatory Python/FastAPI, separate vector database, Redis, Docker, event-stream platform, or collaborative filtering. Any such proposal needs measured evidence and owner approval.
- No claims of Netflix-grade, collaborative, trending, production quality or real customer intelligence from this synthetic dataset.
- Do not modify the shared V2 Drive artifacts or their permissions.
- Never commit API keys, credentials, real customer data or LocalDB secrets.

## 12. Delivery and reporting format

Khánh's final status must be exactly one of:

- IMPLEMENTATION_COMPLETE — READY_FOR_OWNER_REVIEW
- PARTIAL
- BLOCKED

The handoff report must include:

1. Branch name and exact base/HEAD commit SHA.
2. Changed files and a concise architecture/data-flow description.
3. Implemented API/service contracts and how Khang consumes the service.
4. Exact commands executed and their environment.
5. Test names/counts and actual PASS/FAIL/SKIP results.
6. Purchase eligibility and temporal-cutoff rules actually implemented.
7. RFM scoring and segment thresholds.
8. Account-isolation evidence.
9. V2 EF schema source-sync status.
10. Known limitations, blocked acceptance IDs and owner-run integration steps.

Do not report tests that were not executed. Do not describe a code review as a runtime test. Do not claim the V2 LocalDB integration passed from unit tests or a successful build.

---

## 13. V2 reference artifacts

- [Dataset V2 folder](https://drive.google.com/drive/folders/1aCVvSGGsMatv3pmAnY-IM4OGT1VzfMrs)
- [Shared Khang/Khánh data contract](https://drive.google.com/file/d/1VLPojRf0zpNh2aSlgHtT_FzODcAOwmRw/view)
- [Recommendation evaluation split](https://drive.google.com/file/d/1y9SIdvx1L-HyEiyzyaNo4WlJvuG8nx_5/view)
- [Recommendation evaluation notes](https://drive.google.com/file/d/18oPVCsK5YqUVZOPFpDo5rCe8oeS5DeR_/view)
- [Customer transaction mapping](https://drive.google.com/file/d/168_QLBLTCAvp2zS1up0dMeIp7LeF2s82/view)
- [Product catalog mapping](https://drive.google.com/file/d/1Nd_QVLysDjC2zyjPrlplvxl24KsXlHvY/view)
- [Data quality report](https://drive.google.com/file/d/1p_ZVQZo2a05oNzQzfOxpG1MBNnPPb2zq/view)
- [SQL validation output](https://drive.google.com/file/d/1UpQdhF-8Z3CfFEi8MTZdG46VKWomTezf/view)
- [V2 README and safety contract](https://drive.google.com/file/d/1InUnTngLAqD9AvtK-rmi13U1k6CL5N5V/view)
- [Khang's Lane A issue](https://github.com/nhuy2005-alexandors/Marketplace/issues/1)

## 14. Initial handoff status

- Dedicated branch created from the pinned Khang branch commit.
- This handoff document is the only intended change in the Khánh branch at handoff.
- No application code was modified.
- No database was accessed or mutated by this handoff.
- Khánh's GitHub username is not yet known, so the GitHub work item must remain unassigned until the owner supplies/identifies the correct account. Do not assign it to Khang or guess a username.
- The V2 EF source-sync gap is an explicit dependency, not permission for Khánh to invent migrations.
