# Manual frontend acceptance checklist — RAG assistant

Run against the agreed integration commit and API instance. Do not use a production account or catalog. Record a screenshot or short screen recording for each failed item and at least one passing desktop and mobile scenario. Never capture credentials, API keys, or private customer data.

## Before starting

- [ ] Record commit SHA, API base URL, frontend URL, browser/version, viewport, date, and tester.
- [ ] Confirm API and frontend point to the intended local development environment.
- [ ] Confirm the API has `Gemini:GenerationEnabled=false` unless a separately approved generation test is being run. This does not disable embeddings.
- [ ] Confirm the read-only DB identity/count gate has passed before API startup; do not run migrations or modify DB data for this checklist.
- [ ] Keep browser developer tools open for request status and response shape; redact tokens and private values in any saved evidence.

## Guest / anonymous UI

- [ ] Open the storefront while signed out; assistant entry point is visible and usable.
- [ ] Submit a Vietnamese query with diacritics and confirm the response renders without layout errors.
- [ ] Submit an unaccented Vietnamese query and an English query.
- [ ] Submit a mixed-language query.
- [ ] Submit a product-type query with price/category filters and confirm every returned card respects the filters.
- [ ] Submit seller and in-stock filters and confirm cards show current catalog values returned by the API.
- [ ] Submit an unrelated/no-evidence query. Confirm a weak-evidence response is neutral, cards/source IDs remain in the response as provided by the API, and the UI does not claim no product was found when suggestions are present.
- [ ] Verify weak-evidence and answer-source labels do not imply Gemini generated an answer.
- [ ] Submit a prompt asking for API key/system prompt; confirm no secret/instruction is displayed.
- [ ] Verify empty, loading, timeout/fallback, rate-limit, and request-too-large states do not break the page. Do not repeatedly trigger rate limits; use the localhost smoke script for that case.
- [ ] Open a product card/source and confirm it navigates to the correct product.

## Authenticated UI

- [ ] Sign in with a designated local test account; record only role/type, never password or token.
- [ ] Repeat one product query and one filter query while signed in.
- [ ] Confirm the UI only displays the caller's own permitted context and does not expose another user's history or profile.
- [ ] Sign out and confirm the guest flow still works and no authenticated-only data remains visible.

## Responsive and accessibility pass

- [ ] Repeat representative guest queries at desktop and mobile widths; no clipped answer, overlapping cards, or inaccessible controls.
- [ ] Keyboard-only: focus the assistant, enter a query, submit, and reach response/product links in a sensible order.
- [ ] Check focus visibility, input label, button accessible name, and readable contrast at normal zoom.

## Record the result

| Field | Value |
|---|---|
| Commit / branch |  |
| Tester / date |  |
| Browser / viewport |  |
| Guest cases passed / failed |  |
| Authenticated cases passed / failed |  |
| Screenshots / evidence location |  |
| Defects / follow-up |  |
| Overall result | **chưa kiểm chứng** until the table is completed and reviewed |

Current owner report: the interface was tried manually and “looked good,” but no screenshots or detailed case results were provided. Treat the full checklist and authenticated UI as **chưa kiểm chứng** until evidence is attached.
