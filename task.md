# Active objective — APPROVED Hybrid AI Integration, 2026-11-16

## VERIFIED course deliverables completion — 2026-11-17

- Course branch: `codex/marketplace-course-db-completion-20261117`, starting HEAD `53dfd0bbd4ef676c7fe14d16685e26048d1c51a5`. Existing worktrees preserved; no VNPAY, Payment, Refund, seeder or table/column changes.
- Fixed two observed failures: fallback top-product projection was not translatable by the non-SQL Server provider; SQL test expected Paid after the seller service had changed the order to Shipped. Added an explicit INSERT snapshot rejection assertion (SQL 51006). Bounded source review found no remaining blocker.
- Ordinary backend suite: 369 PASS, 0 FAIL, 3 opt-in SQL SKIP. Course SQL test separately PASS on a disposable named LocalDB with all 17 migrations, both procedures and INSERT/UPDATE/DELETE trigger coverage. The owned disposable database, files and instance were removed. Frontend: 10 tests PASS, build and lint PASS.
- Existing `(localdb)\MarketplaceRefundDev20261113 / ECommerceDb_Dev` backed up with COPY_ONLY CHECKSUM, then RESTORE VERIFYONLY WITH CHECKSUM succeeded. Backup SHA-256 `a27e191bc2c0026796dc68e4722f7cb0110ba454c77ab0b8b615af43b622921f`. Backup remains outside Git.
- Applied migration `20261117130000_AddDashboardStoredProcedures` using EF Core SQL Server provider and explicit database GUID guard. All 17 migration IDs applied, pending model changes false. Exact row counts and SHA-256 of all 16 business tables matched before/after migration. No reset, reseed or import. Browser logins only produce normal authentication state changes.
- DashboardService and real API/browser called both procedures; SQL cached execution counts reached 4 each during capture. Trigger metadata includes INSERT, UPDATE and DELETE. Persistent counts remain 5011 orders, 4710 payments, 500 products and 1 refund.
- Updated `docs/BAOCAO.md`, embedded 12 real screenshots under `docs/images/course-20261117`, and produced `docs/BAOCAO_MARKETPLACE.docx` (21 pages). The packaged LibreOffice renderer was unavailable; Microsoft Word rendered the actual DOCX to PDF and bundled PDFium generated page PNGs for visual QA. No fake screenshots or mock API.
- Runtime kept at frontend `http://localhost:5173`, API `http://localhost:5215`. Seller fixture scope is empty; report says so. AI screenshot: HTTP 200 and four correct products above 1000 USD, lexical-warming/fallback; Gemini generation and completed semantic warmup are not claimed. VNPAY QueryDr99 and external refund limitations remain.
- Evidence and backup: `C:/Users/HUY/Documents/Marketplace-Course-Completion/20261117/`. PR #8 remains Draft and must not be merged by this task. Final tested source SHA and artifact checksums are recorded in external completion metadata.

## VERIFIED correction — USD price suffix and boundaries, 2026-11-16

- Bounded source review found no concrete issue in the supported syntax. The reviewer read the parser diff, merge callers and SQL predicates; provider, database and browser checks were not independently rerun.
- Recovery check: backend 367 PASS, 0 FAIL, 3 guarded SQL SKIP. The exact query returned 1784.03, 1100.40, 1252.80 and 1165.72 USD, all above 1000. Frontend HTTP 200 and hybrid retrieval were observed.
- Root cause: the old parser accepted a leading `$` and trailing `USD`, but ignored a trailing `$`. The fix accepts both positions, US/Vietnamese grouping, decimals and strict boundaries.
- Malformed amounts and overflow throw DomainException and become HTTP 400. Strict `above`/`below` uses a one-cent offset against SQL decimal(18,2); the SQL-maximum edge is covered by a contradictory-bound short circuit.
- Tests: valid RED 19 FAIL/4 PASS on the old parser; fixed suite 368 PASS/0 FAIL/3 guarded SQL SKIP (371 total), including the SQL-maximum strict-boundary regression. Live API/SQL matrix 7/7 PASS, including the screenshot query, grouping, range, recommendation follow-up and malformed amount.
- Original 26 files, commerce/domain/persistence/order/payment/refund/frontend source and embedding cache were preserved. No database write, migration, reseed, new database or remote publication was part of the source fix.
- Evidence: `C:/Users/HUY/Documents/Marketplace-Local-Completion/20261116/assistant-price-151647/`. No push or merge was made before this request.

## VERIFIED correction — Vietnamese category requests, 2026-11-16

- Owner screenshot reproduced: `tìm sách cho tao` returned no-evidence despite60Books&Learningproducts. Strict pre-retrieval gate rejected the untranslated category before semantic search. Earlier demo acceptance missed this common Vietnamese query; previous broad assistant readiness does not cover arbitrary natural-language requests.
- Targeted fix: CategoryAliases in existing AssistantDomain options/config for8currentSQLcategorynames; infer only IDs of categories actually present; carry CategoryId in internal typedconstraint and apply SQLfilter before retrieval/recommendation. Expand retrieval with SQLcategoryname, preserving original query and hard budgets; no model may invent catalog IDs/facts.
- Alias overlap is resolved by mention containment (`máy tính bảng` outranks contained `máy tính`), not longest across independent categories. Multiple categories or supported direct negation (`đừng tìm sách`, `tôi không muốn tìm sách`) return needs_clarification rather than a positive product filter.
- Actual RED->GREEN:9Vietnamesequeryregressions initially9FAIL; tablet-overlap1FAIL; reviewnegation/multicategory2FAIL; missingmuonnegation1FAIL. Latest fullsuite351PASS/0FAIL/3guardedSQLSKIP(total354), assistant-vietnamese-verified.trx. The3SQLfacts were separately3PASS in priorauthorizeddisposable run; not rerun for thisAI-onlyfix.
- Actual live acceptance10/10: screenshotquery,unaccentedbudget,bookrecommendation,followup,computer,tablet,clothing,home,2clarifications. Browser exactscreenshotquery rendered4Books&Learningcards withactualGemini/hybrid; no mockserver. Postreviewlive rechecks exactquery and `tôi không muốn tìm sách` on finalbinary. Model responses remain variable/occasionallyslow; thisfix does not claim full conversational fluency orlatencyoptimization.
- Source scope7files pluscheckpoint/docs only; Domain/Persistence/migrations/Order/Payment/Refund untouched. Original26pinnedbytes preserved. Cache1395vectors SHA51f62d8bfda768aee9e69c77d05b4c4f5b4bdcdf8b4c6ac9907c1a12de14ff66 unchanged. No DBmutation,migration,reseed,ornewDB issued bythiscorrection. Ownercontinuedtesting maychangecommercecounts; currentSQLreadbackOrders5011Payments4710Products500Refunds1Migrations16 is an observation, not attributed tosourcefix.
- Branch codex/marketplace-hybrid-ai-20261116, base70733428dad7aa8697f3f9f7dcde10dea8e626a3; correctioncommit recorded in external evidence COMMIT.json afterverifiedlocalcommit. No push/merge. Evidence C:/Users/HUY/Documents/Marketplace-Local-Completion/20261116/assistant-vietnamese-142923/ . API5215restartedexplicitprivateconfig; frontend5173kept. Usercanresubmitthequeryintheexistingassistantpage.

## VERIFIED — local demo integration accepted, 2026-11-16

- CURRENT STATE: Hybrid AI integration and bounded local demo acceptance completed. Earlier quota-blocked/current-progress sections below are HISTORICAL and SUPERSEDED by this checkpoint. No remote publish/merge.
- Active worktree C:/Users/HUY/.codex/worktrees/marketplace-hybrid-ai-20261116/E-Commerce; branch codex/marketplace-hybrid-ai-20261116. Final local commit is recorded in external evidence INTEGRATION_COMMIT.json; use current git HEAD to identify the checkout. Parent preservation commit de9e2c957b41094ec9668b294649f35b92fcdccb contains all26 local dirty files including VNPAY.
- Owner supplied a new Google key in original CLAUDE.md labelled key moi. Safely extracted to private LOCALAPPDATA/Marketplace/AI/embedding.local.json, nativeGoogle200/768finite verified. Original CLAUDE user edits preserved; no keys copied into integration source. Existing localproxy generation remains gemini-3.8-flash at127.0.0.1:8318/v1.
- Completed1500catalogchunks/500products with1395distinct768-dimensional vectors; SQL-derived requiredhash coverage1395/1395. NativeGoogle calls real. Cache-restart SHA unchanged and APIhybridReady=true/modehybrid after restart; no catalog re-embedding. Startup script kept explicit MarketplaceRefundDev20261113/ECommerceDb_Dev target.
- Latest actual source verification: solutionbuild0warnings/errors; backend338PASS/0FAIL/3guarded-disposableSQL SKIP(total341), hybrid-final-metadata.trx; frontend10PASS/buildPASS/nativeoxlintPASS. No testdisabled. The3SQLtests cannot target persistentdevelopment DB and were not run.
- Actual final10caseAPI probePASS: USDcaps, recommendationbudget, carriedfollowup, explicitfilters, missingSKU, out-of-scope, HTTP400invalidfilters, blockedDBaction, liveGemini selection and RAM>=16/storage<=512 exactSKU1. All returned price/stock/SKU/brand/attributes match actual productendpoint. GenerationcaseanswerSourcegenerated, retrievalhybrid. Honestfallback/weak-evidence cases retained.
- Actual finalbrowserPASS: GuestAssistantgenerated+hybrid+productcards/noerror; CustomerA1022personalized4; queryinput pendingtrue beforelogout, B1024login on samebrowser -> ownorders0,noAorder6007,noAchat. AdminRFM200/1002customers/7segments, CustomerRFM403, Sellerownorders200. Nativebrowser/realAPI/realSQL; no mockserver for these acceptance cases.
- Runtime found RecommendationDTO omission of BrandName/attrs: RED1FAIL, fixed2Includes inLoadCatalog, now maps allactivecatalogattrs while ranking stillcomparable-only. Full338PASS and repeated10livecasesPASS. Reviewer read exactfix/test, no concrete residual. F01-F10 corrections and follow-up/empty-model/quota fallback fixes have regressions and boundedread-only review.
- DBstate: orders5008,payments4707,orderitems12508,refunds1. Integrationfixturesonly newB1024 and CODorder6007; browsercheckout/cancel stock49937->36->37. Orders5006Pending/5007Paid and refund1Rejected remain. No newgatewaycapture/externalrefund. InvalidVNPAYIPN97/publicproductAPI404verified. ExistingQueryDr unsigned99 remains unresolved; not a newAIintegrationblocker or a positivequeryacceptance.
- SQLmodelalignment16/16,no pendingmodelchanges,FKdisabled/untrusted0,SKUblank/duplicate0. Domain/Persistence/Order/Payment/Refund/VNPAY/DbSeeder untouched relativepreservedbaseline. OriginalHEAD e683293 and original26hashes reverified unchanged. Exact4privatekeyvalues absent from integrationcandidatefiles. No newDB/migration/reseed/restore/export.
- Evidence: C:/Users/HUY/Documents/Marketplace-Local-Completion/20261116/hybrid-integration/INTEGRATION_REPORT.md, CHANGED_FILES.txt, test-results/, runtime-ai-results-final.json, browser-ab-final-result.json, browser-live-hybrid-final.png, cache-restart-after.json, sql-cache-coverage-final.log, final-preservation-and-secret-check.json . Full source/start instructions docs/HYBRID_AI_LOCAL.md.
- Runtime kept API5215 and Vite5173, LocalDBinstance and narrowIPNbridge/tunnel alive. StoponlyownedAPIbeforebuildingDLLs onWindows. On futurewarmupfailure, lexicalfallbackstayslabelled; restartafterproviderrecovery torewarm. No automaticunattendedretryframework, noofflineAIclaim, noproductioncertification.
- Remaining decisions belong to Owner: review final local source/evidence and decide any remote publication. No further AI integration implementation milestone is pending for this bounded demo scope. Known provider QueryDr and3skippedSQLtests remain explicit limitations, not claimedPASS.

## Current verified checkpoint — quota blocker, 2026-11-16

- NOT COMPLETE: semantic/hybrid acceptance blocked by Google embedding per-day quota. Actual HTTP429 RESOURCE_EXHAUSTED: EmbedContentRequestsPerDayPerUserPerProjectPerModel-FreeTier, limit1000. Persistent real cache992 vectors,768dimensions. New key in the same project does not reset this quota. Owner asked whether another account/key needed; answered same project billing upgrade or wait reset. No credentials/project/provider route changed.
- Root runtime current session verified gpt-6.1-sol/xhigh using native turn_context. Original26 pinned file bytes + originalHEAD e683293 unchanged in fresh preservation scan; Domain/Persistence/migrations/Payment/Refund/Order/Checkout have no diff from preserved integrationbaseline. Exact3 private key values scanned across Git candidate files: no matches.
- Reviewer concrete residuals corrected with RED7FAIL -> GREEN: carried followup budget before recommendation; up to/at most/maximum/no more than caps; numeric field-after-amount binding within clauses. Legacy and V2 contextualizers preserve raw currency/decimals; both covered. Review re-read found no residual for these items.
- Runtime quota exposed per-chat embedding retries after failed warmup. REDcallcount -> existing lexical fallback now used after known failed warmup. IsHybridReady remainsfalse and modelexical. Restart is INTENDED recovery after quota returns; no unattended background retry added. Reviewer confirmed this known limitation.
- Actual live query exposed empty Gemini selectedIDs erasing strong SQL evidence. RED1FAIL -> removed early-return; empty/invalid IDs use SQL fallback with answerSource=fallback. True empty candidates stillnoevidence. Existing Shared model-veto test updated to authoritative catalog contract, not disabled. Reviewer read actualdiff/test and found no concrete residual.
- Latest actual full backend337PASS/0FAIL/3SKIP, total340, test-results/hybrid-final-acceptance.trx. Skips remain disposableSQL migration/rollback/concurrency facts; no guard bypass/newDB. Current frontend10PASS/0FAIL/0SKIP, buildPASS, nativeinstalledoxlint exit0. No frontend changes after these runs.
- Actual browser customerA1022: Product499add -> Cart -> CODcheckout -> newOrder6007Accepted/PaymentPending -> browserCancel. SQLstock37->36->37, order6007Cancelled. Order5006 remainsPending,5007Paid,Refund1Rejected unchanged. Keep retained auditorder, no SQLdelete/reset.
- Actual browser Admin login/RFM200:1002customers,7segments. Sellerlogin/ownorders200(empty synthetic seller). CustomerRFM403. A->B test: started actualchat whilepending,logout,loginB1024 on samebrowser; B ordersHTTP200/total0,no A order6007; Assistantempty,no A query. NewB is only newcustomer fixture in this run; auth normal refresh-token rows also created.
- runtime-ai-probe.py initial run found live model-emptyselection bug; original resultsfile remains current pre-fix evidence until rerun. Contradictory explicitfilters correctlyHTTP400; initial probe expected200 incorrectly. Probe script corrected expected400 and tightened positive nonempty assertions, NOT YET rerun after sourcefix. semantic queries remainNOT ACCEPTED duequota.
- Integration branch codex/marketplace-hybrid-ai-20261116; HEAD de9e2c957b41094ec9668b294649f35b92fcdccb plus uncommitted Hybriddiff. No final integrationcommit/push/merge. Originalworkspace untouched, DbSeeder untouched,16migrations intact.
- RuntimeAPI restarted on5215 viaStart-LocalApi using originalprivateconfig+privateAIkeys. Frontend5173 kept; SQL instance and narrowIPNbridge/tunnel unchanged. Do not rebuild while ownedAPI locksDLLs.
- Remaining: after quota owner resolution/restart, finish missing vectors using persistentcache, verify actualhybrid retrieval+Gemini selections/spec/followup, rerun corrected liveprobe; recheck business state/preservation, complete as-built report/localcommit/startcommands. No newDB/migration/reseed/remote publish.

## Runtime integration progress — 2026-11-16

- Hybrid fixes implemented root-side with tests: F01 accented USD budgets; F03 Gemini selects IDs only/backend renders SQL facts and rejects mixed IDs; F04approvedseller; F05shared PurchaseEligibility for rec/RFM withPaidAt cutoff; F07validorder profile;F08IsComparable;F09clause-local quantity/comparator;F10explicit filters. F02filtered candidateIDs passed into shared ranking before topK; red fixture beyond20 proves true ranking path. Cold-start catalog fallback labels honest.
- Gemini user provided local OpenAI-compatible proxy, saved outsideGit LOCALAPPDATA/Marketplace/AI/gemini.local.json (BaseUrl8318/v1,GenerationApiKey private); directlivegeneration200 valid IDs. Google key later found in user-edited originalowner CLAUDE.md(line20) with nonstandard keyprefix; copied privately to embedding.local.json. DirectnativeGoogleembedding200/768finite. OriginalCLAUDE user edit preserved,neverprinted/staged/copiedintointegration.
- New Gemini provider supports independent localBearer/chat generation while Google embedding stays native. Transport testRED->GREEN. scripts/Start-LocalApi.ps1 loads original private config into processenv + two privateAIsecrets,explicit DBtarget guard,nevercopies credentials.
- Fullsuite initially327PASS? Actual final fullsuite:327PASS/0FAIL/3disposableSQLSKIP (hybrid-full-host-isolation.trx). Staticwarmup state caused cross-host unit interference; separatehost regressionRED reproduced; moved onlyexisting cache/warmup state into DI singleton perhost, scopedsearch remains. No testdisabled.
- Ownerfrontend integrated from frontendworker23dd7c5 (10allowlistedfiles), rootreadreal diff; VNPAYhooks preserved. OwnerAssistantconversation mapped toSharedDTO; ABabort+identityRef,privatequerykeys,userboundRFM. Rootnpmci0vuln,frontendbuildPASS,npmtest in progress, lintnativeoxlint exit0; npmrunlintwrapper EOF issue bypassed by direct installednodeoxlint command without packagechanges.
- Actual readonlySQLprobePASS:16applied migrations matchsource,no pendingmodelchanges,unchangedOrders5007Payments4706Refunds1,rec/similar/RFM queriestranslate onSQLServer. RFM1001customers; actual authenticatedAPI:guestRFM401/customer403/seller403/Admin200; customerrecommendationpersonalized4,guestpopular10,similar2.
- Stoppedonly verified previousAPI19140+Vite2904; restartedintegrationAPI5215 session3892 viaStart-LocalApi; Vite5173session33773. Existing narrowIPNbridge5316+cloudflared kept. APIbackgroundGooglewarmup88batches,16documents/batch,3spacing; persistentcache privately atLOCALAPPDATA/Marketplace/AI/hybrid-embedding-cache.json. First176entriesfinite768verified; warmupstillongoing. AvoidrebuildwhileAPIlocksDLLs;stopownedAPIbeforebuild.
- Browserhybrid-guest open /assistant; firstreal query submitted 'Tìm FictionPlay dưới600USD'; awaitingresponse. Authsessionsprivately atLOCALAPPDATA/Marketplace/AI/runtime-acceptance-sessions.json (do notprint/readraw), from existingcustomer/seller/admincredentials. No newcustomeraccount/checkout/payment yet.
- Fresh read-only reviewer /root/hybrid_final_review running on actualdiff. Next:consume findings/fixTDD; finishwarmup+liveAI8-12queries+followup/specs;realbrowserroleflows+ABrace+criticalcheckout/VNPAYsmoke;keep5006Pendinguntouched;recordbefore/afterDB;finalbuild/test+localcommit/docs/commands. No pushmerge. Original26hashes+HEADreverifiedunchanged.


- Original preserved: C:/Users/HUY/.codex/worktrees/marketplace-owner-local/E-Commerce, HEAD e68329316392cc72142c147de0a2fb88e8d05bfc,26 dirty files byte-pinned outside repo. Never edit/reset/stash that tree. Preservation record: C:/Users/HUY/Documents/Marketplace-Local-Completion/20261116/hybrid-integration/BASELINE_PRESERVATION.json .
- Active integration worktree: C:/Users/HUY/.codex/worktrees/marketplace-hybrid-ai-20261116/E-Commerce ; branch codex/marketplace-hybrid-ai-20261116. Baseline commit de9e2c957b41094ec9668b294649f35b92fcdccb includes ALL26 local dirty/untracked source files incl VNPAY. No push/merge.
- User approved: Shared99d870ad08472d7749c5d0deebbe3dbce725568c RAG/chunks/persistent cache/context/provider/Recommendation ranking; Owner0e3538bfb8155f036d2a8c819474a15fc850892e purchase eligibility/RFM/UI. Keep16 migrations, Domain/Persistence/payment/refund/VNPAY/business unchanged. Fix reviewF01-F10 with TDD, no duplicate engines/newDB/reseed/schema changes.
- Transplanted26 Shared AI/source/tests and9Owner AI/UI/RFM files only. Backend DI/config/AI rate limit merged into baseline; VNPAY registrations/hooks retained. FrameworkReference Microsoft.AspNetCore.App supplies existing shared dependencies. Build integration initially exposed missing references and incompatible test IRfmService signature; these mechanical issues fixed. Targeted transplanted tests running; no application behavior fixes yet.
- F03 approved implementation direction being critiqued: Gemini may select/order valid candidate IDs; backend renders factual catalog price/stock/spec data from SQL DTO,never trusts arbitrary generated prose. Report live generateContent vs lexical fallback honestly. One eligibility helper for Recommendation/RFM; pending fulfillment item in paid order remains eligible, cancelled items excluded.
- No Gemini key found in current local config/environment or known original checkout/UserSecrets sources. Async owner input requested: save {"ApiKey":"..."} at LOCALAPPDATA/Marketplace/AI/gemini.local.json (empty template created),or give existing path/env name; never paste key in chat. Continue independent implementation while waiting.
- Evidence directory: C:/Users/HUY/Documents/Marketplace-Local-Completion/20261116/hybrid-integration/ . Review specification/probes: ../ai-code-review/AI_CODE_REVIEW.md .
- Runtime target MUST remain (localdb)\MarketplaceRefundDev20261113 / ECommerceDb_Dev. Original app ports5215/5173 and narrow IPN bridge5316 may still run; do not kill without verifying owned process identity. Switch to integration binaries on same ports only after build,using original private config via child-process environment (do not copy credentials to new repository). VNPAY query99 remains unresolved,Order5006Pending must not be promoted/cancelled/recreated.
- Required remaining work: red regressionsF01-F10 -> minimum fixes -> Owner frontend contract/identity integration -> build/full appropriate regression -> real SQL read-only alignment/AI queries -> live Gemini with actual key -> browser roles/AI/account switch/checkout-VNPAY business smoke -> fresh independent review -> local commit + final evidence/commands. No remote publish without acceptance.

---

# Current checkpoint — Owner local audit, 2026-11-15

## Current follow-up — 2026-11-16 VNPAY query investigation

- VERIFIED: existing 26-file source hash pin matched before this checkpoint update; no application/config/schema changes. Fresh existing-assembly VNPAY regression: 25PASS/0FAIL/0SKIP. No rebuild or full regression repeated in this diagnostic follow-up.
- VERIFIED: read-only queries for Pending5006 and Paid5007 both return HTTP200 with the same unsigned99 body. Numeric13 RequestId, numeric JSON timestamps, actual numeric TransactionNo and application/json without charset do not resolve it. An intentionally invalid-checksum query returns the identical body too; none establishes authenticated payment status. No persistent SQL write or new payment/refund.
- VERIFIED: SIT login works, but its linked /order/search returns a Whitelabel Forbidden403. Old merchantv2 notice concerns Link thanh toan migration only; it does not prove QueryDr moved. Merchant View delegates to VNPAY ID phone login; no phone/OTP flow submitted. UNKNOWN: whether QueryDr99 is a merchant entitlement, backend fault or another unisolated provider issue. Do not present the refund-only SDK issue as proof for QueryDr.
- Runtime from the prior turn had stopped. Restored actual API5215/frontend5173/limited bridge5316 with explicit isolated Development connection; Program.cs has no startup migration/seeder. API categories/products and browser catalog render PASS. Sessions: API63628, frontend39635, bridge8378, tunnel2065. Keep these services running.
- New temporary HTTPS origin: https://struct-demands-cole-bold.trycloudflare.com . Exact GET IPN is /api/payments/vnpay/ipn. Correct SIT terminal updated and persisted URL read back; public landing200, Admin/products404, invalid-IPN97. Previous sun-machine URL is SUPERSEDED. Only landing and IPN are exposed.
- SQL readback unchanged: Orders5007, Payments4706, OrderItems12507, Refunds1, migrations16, Product499stock37;5006Pending,5007Paid/non-demo/no-reconciliation. Keep5006 unmodified until authenticated provider confirmation. Evidence: C:/Users/HUY/Documents/Marketplace-Local-Completion/20261115/owner/vnpay-query-20261116/ . No commit/push/PR/merge; original DB/worktree and backups not targeted.
- Diagnostic follow-up COMPLETE, live QueryDr UNRESOLVED: published @nodepay/vnpay0.1.0 (NPM integrity verified) also uses the same endpoint and string timestamps; no demonstrated source defect identified. REPORT.md and VNPAY_SUPPORT_REQUEST_UNSENT.md prepared with requestId/time for provider tracing; not sent. Browser sessions closed and temporary auth-vault profile deleted. API/frontend/IPN tunnel remain running. Prior whole-suite results remain historical; fresh scoped25PASS was actually run.

## Active follow-up — VNPAY Sandbox (implementation verified; live query limitation)

- Owner authorized independent VNPAY provider and account registration, preserving MoMo/lifecycle and current work. Branch/HEAD below unchanged; implementation remains uncommitted and preserves pre-existing MoMo HTTP fix/tests.
- Implemented sandbox URL HMACSHA512, USD25637→wholeVND→wire×100, immutable alphanumeric reference with UTC timestamp, read-only signed Return, GET IPN with exact RspCode/Message casing, owner/Admin authenticated query, pending/final distinction, duplicate/late/failed/reused-ID reconciliation holds, real socket IP, request validator and checkout/query UI. No schema/migration/new DB/seed.
- Latest checks after User-Agent correction: build0warnings/errors,188PASS/3disposable-onlySQL SKIP. Frontend8PASS,build/lintPASS. User-Agent regression RED4fail/1pass→VNPAY25PASS. Root read actual diff/logs; independent read-only review/correction rereads found no remaining concrete defect in scoped code. No SQL locking/concurrency claim from fixture tests.
- Registered successfully through new official https://pay.vnpaytest.vn/dev-registration (HTTP201), after Owner completed reCAPTCHA. Old devreg404 and GitHub website409 resolved by using actual project HTTPS landing. Owner activated email and entered real sandbox TmnCode/HashSecret in gitignored Local.json; presence/format checked without printing values. Private registration credentials: LOCALAPPDATA/Marketplace/VnpaySandbox/registration-credentials.json; never copy into reports/source.
- Live synthetic Order5005 was created during validator/credential smoke and cancelled via browser; no Payment5005-by-order, stock restored39. Order5006 completed official NCB test-card flow and returned signed ResponseCode00/TransactionStatus00 with positive provider transaction; Return correctly left DB Pending. Query remains unconfirmed. DO NOT cancel/recreate5006 or fake IPN/capture while authoritative confirmation is pending.
- Correct SIT terminal IPN was initially#, then saved to the verified public HTTPSbridge. SeparateOrder5007 completed sandbox card flow after IPN setup; SQLPaymentCompleted/OrderPaid at2026-11-15T18:10:15Z, IsDemo=false, RequiresReconciliation=false, captureIDpresent. Returnnevermutates and everyqueryremainedunauthenticated, so IPN is the source-correlated explanation; no raw inboundpayloadtrace preserved (bridge receiptlogging added after capture).
- CustomerbrowserRefund1 forOrder5007→Pending/RefundPendingtrue; realAPI Adminship/cancel/duplicaterefund409. AdminbrowserReject→Rejected, holdfalse, OrderPaid/PaymentCompleted unchanged; stock37 unchanged. No externalrefund/Complete, fakecapture or restock. Query99 remains known provider-side/unisolated failure, no positive livequeryacceptance.
- Live query initially403 because .NET lacked User-Agent. Controlled .NET empty-query probe: absentUA403HTML vs Marketplace-Sandbox/1.0 UA200JSON. Added transparent UA+Accept headers with RED→GREEN. Query now reaches provider but returned unsigned code99 Unexpected error; no DB mutation. Empty/zero optional transaction-number probes did not fix99. Diagnose actual API/merchant setup, preserve guards.
- Public bridge: https://sun-machine-jersey-skip.trycloudflare.com ; only landingGET / and signedGET /api/payments/vnpay/ipn forwarded to API5215; Admin/products paths404 externally. Portable cloudflared2026.11.0 official SHA256 verified. Bridge outside repo in evidence, self-testPASS. Sessions kept: bridge86565, tunnel97870; API39463; Vite36089. Quick Tunnel is temporary, update registeredIPN if it changes.
- All five dedicated registration/merchant/SIT/customer/admin browser sessions closed; two local auth-vault profiles removed. No cookies/headers/HAR export. Private VNPAY credentials remain local only. Direct TerminalEdit.htm without selection was not saved; actualIPN configured/readback on matchingSITterminal.
- FinalDB: Orders5007,Payments4706,Refunds1Rejected,OrderItems12507,16migrations; Product499stock37 due retainedOrder5006pending+Order5007paid. All pre-VNPAY Orders/Payments/OrderItems hashes match baseline. No newDB/schema/seed/restore/backups/originalworkspace changes. Current report/manifest/source hashes: C:/Users/HUY/Documents/Marketplace-Local-Completion/20261115/owner/vnpay-20261115/ . Docs: docs/VNPAY_SANDBOX.md and docs/tech_specs/2026-11-15-vnpay-sandbox.md. No commit/push/PR/merge.

## Owner audit checkpoint retained

- Active scope: Owner DB/business only. RAG and recommendation/classification await corrected AI lanes; no AI source imported.
- Local branch: codex/marketplace-owner-local-20261115, based on main 540d296836be789a58ede9db1039447d95a3c9cc. No remote push/PR/merge.
- Existing DB: (localdb)\MarketplaceRefundDev20261113 / ECommerceDb_Dev; all 16 migrations match source, no pending model changes. No new DB/instance, reseed or restore.
- One source audit found/fixed seller payment leakage, inconsistent cent allocation, hidden-product reviews, concurrent cart increments/inserts and seller fulfillment controls during holds. Also fixed cart integer overflow and StartupSafety test isolation; patched frontend lockfile dependencies.
- Verification: backend 159 passed/3 disposable-only SQL facts skipped; frontend 6 passed, build/lint passed; npm audit 0 vulnerabilities. Fresh SQL probes cover existing-cart increments, first cart/item races and snapshot-trigger rollback. These probes are not substitutes for refund/callback concurrency facts.
- Actual browser: Admin login/dashboard/catalog, Customer add-to-cart/COD checkout/cancel, Seller own-order view. Dedicated browser/auth vault profiles cleaned.
- Audit fixtures retained: Customer1022, Seller1023, Orders5001(cancelled COD),5002(paid demo),5003(cancelled COD). Product499 stock39 from40 due one retained demo purchase; other stock/SKU unchanged. Temporary catalog/coupon fixtures removed through their APIs.
- Online gate retry, 2026-11-15: Owner approved fixed 25637 VND/USD for future transactions. Saved in private Local.json, inherited existing sandbox credentials, restarted API. Browser checkout5004 froze USD505.37 as VND12956171, but payment initiation returned non-JSON; separate GET/empty-POST sandbox probes returned HTTP403 HTML. No successful provider capture was established. Browser cancellation restored Product499 stock38→39; retained Order5004 Cancelled/Payment Failed and immutable reference for the audit trail. No fake capture, refund or external transfer was created.
- Focused payment/refund tests: 38 PASS/0 FAIL/0 SKIP (existing built test assembly, --no-build). Live invalid IPN/callback and demo refund guards PASS. Positive capture/duplicate authenticated callback and refund hold/reject/complete on SQL remain BLOCKED. Refund is the existing manual external-reference MVP, not an automatic MoMo refund integration.
- App URLs: frontend localhost:5173, API/Swagger localhost:5215/swagger. Use the private gitignored Local.json; startup never migrates/seeds.
- Original D:/Dev/Workspaces/E-Commerce and 138 protected files, DbSeeder.cs, V1/V2 backups preserved.
- Report/evidence: C:/Users/HUY/Documents/Marketplace-Local-Completion/20261115/owner/audit-20261115/REPORT.md.
- Online gate evidence: C:/Users/HUY/Documents/Marketplace-Local-Completion/20261115/owner/online-gates-20261115/REPORT.md. Pre-existing Order/Payment/OrderItem row hashes match before the retry; 16 migrations remain unchanged. No new DB, reseed, restore or source logic change.
- Read-only 403 follow-up: documented sandbox DNS/TLS verified, no configured HTTP proxy, User-Agent variants still403. One signed provider status query for existing Order5004 also403 HTML; remote capture remains UNKNOWN. No create retry, new fixture, DB/config or application-code change. Owner has shared demo only; all three configured demo fields match MoMo's official payWithMethod CollectionLink sample. Own M4B is an option, not a proven prerequisite for resolving403. Next discriminator: Owner-controlled network comparison or MoMo denial details; do not assume credentials are invalid or bypass guards.
- Historical comparison: source caa8f97 and current HEAD have identical base MoMo credentials/endpoints/payWithMethod/create-HMAC code. July payment success is reported by Owner and historical project docs (ab571d2); old private runtime/network config is UNKNOWN. Current quote passes; do not attribute provider403 to new callback/refund guards without evidence.
- Follow-up correction: MoMoProvider now reports HTTP failures before JSON parsing and uses a stable invalid-JSON error. No retry/guard/currency changes. Transport regression RED3fail/1pass→GREEN4pass; focused42pass; fresh solution build0warnings/errors, full163pass/3disposable-onlySQLskips. API restarted; no new payment or DB write. Chrome documentation page, native curl and Windows PowerShell status probes also403HTML. Owner has no alternate network now; hotspot comparison NOT RUN. External Test-MoMoConnection.ps1 and unsent MOMO_SUPPORT_REQUEST.md prepared. Root read actual diff/logs; independent bounded review found no concrete defect. HTTP fix/tests/docs remain uncommitted; online gates still BLOCKED.

Historical checkpoint below is retained; older dates/results do not describe current runtime.

---

# Task Checkpoint — MiniShop

Cập nhật: 2026-07-05

## Mẻ hiện tại (2026-07-05, CHƯA COMMIT) — `docs/tech_specs/2026-07-05-marketplace-ui-momo-docs.md`

| # | Task | Trạng thái | Ghi chú |
|---|------|-----------|---------|
| 1 | UI marketplace revamp | ✅ | Palette xanh, header 2 tầng, HomePage (hero/voucher/flash-sale/top-rated/low-stock), Footer link thật, emoji→lucide, max-w-7xl, grid 5 cột |
| 2 | Thanh toán MoMo cổng thật | ✅ | `MoMoProvider` AIO v2 sandbox (HMAC-SHA256, payWithMethod, USD→VND), callback + IPN. Gỡ sạch Stripe + VNPay |
| 3 | Backend hỗ trợ homepage | ✅ | `GET /coupons/active` (public), sort `rating`+`stock` cho ProductService |
| 4 | Gom diagram vào `docs/diagrams/` | ✅ | git mv 6 sơ đồ; sync cross-ref (SRS/DECISIONS/README) |
| 5 | Thêm phân tích bài toán | ✅ | `docs/phan-tich-bai-toan.md` (5 vấn đề cốt lõi, stakeholders, 3 máy trạng thái, scope) |
| 6 | Thêm Component/Deployment diagram | ✅ | `docs/diagrams/architecture-diagrams.md` |
| 7 | Sync docs VNPay/Stripe → MoMo | ✅ | SRS, use-cases, sequence, state-machine, BAOCAO, README |
| 8 | Vá class-erd thiếu | ✅ | Thêm entity `RefreshToken` + enum/field `SellerStatus` |

## Verify (mẻ này)
- `dotnet build ECommerce.API`: 4 project, 0 lỗi/cảnh báo
- `npm run build` (client): sạch
- Smoke test API: `pay {momo}` → `requiresRedirect=true` + payUrl `test-payment.momo.vn`; `pay {vnpay}` → 400 (đã gỡ)
- Thanh toán thật MoMo: chọn Ví MoMo → cổng → thẻ ATM test `9704 0000 0000 0018` → order Paid

## Mẻ trước (2026-07-03, ĐÃ COMMIT `caa8f97`)
Seller onboarding (Pending→duyệt), refresh token (15m access + 7d rotate), rate limiting, integration test (SQLite in-mem, 13 test), email mock, upload MIME sniff, trang shop công khai, payment split, Swagger ProducesResponseType. Đặc tả: `docs/tech_specs/2026-07-03-batch-features.md` + `2026-07-03-hardening-batch2.md`.

## Treo (chưa fix)
- MoMo IPN không tới `localhost` — dev chốt đơn qua redirect callback; deploy public/ngrok mới chạy IPN. Chi tiết: `docs/tech_specs/2026-07-05-marketplace-ui-momo-docs.md`.
- Payment split: mới breakdown kế toán, chưa payout thật.
- Flash-sale "giá gốc" là tổng hợp (×1.4) — app chưa có field giá gốc.

## Còn lại
| Task | Trạng thái | Ghi chú |
|------|-----------|---------|
| Commit mẻ 2026-07-05 | ✅ | UI + MoMo + docs reorg |
| Push GitHub + CI thật | ✅ | Agent đã push |
| Dọn `commit.txt` staged | ✅ | Đã xoá file nháp |

## Tài khoản demo (sau seed)
- Admin: `admin@shop.com` / `Admin@123`
- Customer: `user@shop.com` / `User@123`
- Seller (Approved): `seller1@shop.com`, `seller2@shop.com` / `Seller@123`
- Seller mới đăng ký qua UI = Pending, cần Admin duyệt tại `/admin/sellers`
- Thẻ ATM test MoMo (thành công): `9704 0000 0000 0018`, NGUYEN VAN A, 03/07, OTP bất kỳ

---
Xem thêm: `docs/specs/` (đặc tả từng feature), `docs/tech_specs/` (as-built theo mẻ), `docs/DECISIONS.md` (ADR), `docs/GOTCHAS.md` (bẫy đã dính), `docs/phan-tich-bai-toan.md` (phân tích bài toán), `docs/diagrams/` (UML).
