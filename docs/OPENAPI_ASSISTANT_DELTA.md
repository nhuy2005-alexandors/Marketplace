# OpenAPI delta — Assistant chat

This delta documents `POST /api/assistant/chat` as implemented in `AssistantController`, `AssistantDtos`, and `ProductDto`. The route is anonymous; a bearer token is optional. Send `Content-Type: application/json`. Names below use the API's camel-case JSON policy.

## Request

```json
{
  "query": "Tìm tai nghe không dây dưới $100",
  "filters": {
    "categoryId": 3,
    "sellerId": 12,
    "minPrice": 10,
    "maxPrice": 100,
    "inStockOnly": true
  },
  "conversation": {
    "conversationId": "optional-client-id",
    "recentTurns": [
      { "role": "user", "content": "I need headphones" },
      { "role": "assistant", "content": "What connection do you prefer?" }
    ]
  },
  "context": { "visibleProductIds": [101, 102] }
}
```

| Field | Type | Validation / behavior |
|---|---|---|
| `query` | string | Required in practice; 1–1000 characters after trim. |
| `filters.categoryId` | integer or null | Must be positive; SQL hard filter. |
| `filters.sellerId` | integer or null | Must be positive; SQL hard filter. |
| `filters.minPrice`, `maxPrice` | decimal or null | Non-negative; min cannot exceed max. |
| `filters.inStockOnly` | boolean | Defaults to `false`; SQL requires stock > 0 when true. |
| `conversation.conversationId` | string or null | Echoed when supplied; otherwise a new ID is returned. |
| `conversation.recentTurns` | array | At most 4 turns; each role at most 16 chars and content at most 500 chars. Only `user` and `assistant` roles are forwarded to the answer provider. |
| `context.visibleProductIds` | integer array | At most 20 IDs; hints only, never retrieval authority. |

The request body limit is 32,768 bytes. The endpoint is rate-limited by the `ai` policy to 12 requests per source IP per fixed minute; the API also has a global limit of 100 requests/IP/minute.

## Success — `200 OK`

```json
{
  "answer": "Các sản phẩm phù hợp với …",
  "products": [
    {
      "id": 101,
      "name": "Catalog product",
      "description": "…",
      "price": 49.99,
      "stock": 5,
      "imageUrl": null,
      "categoryId": 3,
      "categoryName": "Audio & Wearables",
      "sellerId": 12,
      "sellerShopName": "Demo shop",
      "averageRating": 0,
      "reviewCount": 0
    }
  ],
  "sources": [
    { "type": "product", "productId": 101, "chunkType": "summary", "contentHash": "…" }
  ],
  "retrieval": { "mode": "hybrid", "hasEvidence": true },
  "conversationId": "…",
  "evidenceStrength": "strong",
  "answerSource": "generated",
  "intent": "product_search",
  "retrievalMode": "hybrid",
  "constraints": [],
  "contextAction": "none"
}
```

`evidenceStrength` is `strong`, `weak`, or `none`. It is additive; the existing `retrieval`, product and source fields retain their previous meaning and ordering. A weak result is returned from the local Vietnamese response without calling Gemini generation; it keeps the same retrieved products and source IDs in the same order and says they are near the request but may not fully match. It avoids claiming that no product was found when suggestions are present. A strict no-evidence result has no product cards or source IDs and uses `evidenceStrength="none"`. A strong result follows the existing generation path (or its existing grounded fallback).

`answerSource` is additive and identifies how `answer` was produced: `generated` for a valid Gemini response, `fallback` for a local recommendation/grounded response after a provider failure, timeout, open circuit, or when generation is disabled, `weak-evidence` when legacy retrieval evidence is weak and generation is skipped, `no-evidence` for a strict empty result, and `scope-guard` for a fixed response to a non-product intent. Server configuration `Gemini:GenerationEnabled` defaults to `false`; with it disabled, strong-evidence requests use the local grounded response from the ranked candidates and set `answerSource="fallback"`. The answer-generation provider is not called. Embedding/retrieval configuration is independent and remains enabled as configured.

`intent` is an additive RAG-lane field with values `product_search`, `greeting`, `account_action`, `out_of_scope`, and `blocked`. It does not change the shared `ProductDto`, product ranking, order, or source selection. Rule-based scope guards return HTTP 200, empty `products` and `sources`, `retrieval.mode="scope-guard"`, `retrieval.hasEvidence=false`, and `answerSource="scope-guard"`; they do not call embedding or answer-generation providers. Out-of-scope classification first checks for lexical catalog evidence in the remainder of the query; when found, the request continues through the existing product-search path with its original query. Questions about subjective impressions remain `product_search`, skip Gemini generation so the answer does not invent subjective experience, and carry a caveat that only store specifications/reviews are available.

The strict evidence and typed-filter work adds only response metadata: `retrievalMode` (`hybrid`, `lexical-warming`, or `lexical`), `constraints` (recognized typed and request-filter conditions), and `contextAction` (`followup`, `new_topic`, or `none`). When `Gemini:StrictEvidenceEnabled=true` (default), a typed condition is applied as an SQL hard filter before ranking; semantic score cannot restore products that fail a condition. Unsupported units/explicit unknown brands and conditions with no matching products return HTTP 200 with empty `products`/`sources`, `evidenceStrength="none"`, `answerSource="no-evidence"`, and a Vietnamese no-result answer. A known attribute without structured values uses a no-information response rather than guessing. A query without typed constraints must contain a catalog dictionary match (product/category/brand/SKU/attribute/value), or it returns the same empty no-evidence response. An explicit SKU query remains `strong` even when the SKU has no match; no product card or source is returned in that case. The `constraints` field records what the server understood, including price/stock/category/seller filters. No DB schema or migration is added.

`ContextualizerV2Enabled` limits server-side context to the last 3 turns. A short recognized follow-up may merge with the prior user product query; a new catalog subject clears old context. `EmbeddingWarmupEnabled` starts a hosted background warm-up; while warming, product retrieval reports `retrievalMode="lexical-warming"`. `Gemini:EmbeddingBatchDelaySeconds` controls embedding pacing and `Gemini:EmbeddingCachePath` optionally sets a local cache file path. Cache files must stay ignored by Git. The `/api/assistant/chat` response remains additive; clients should display `answer` and omit product cards when `products` is empty.

## Readiness

`GET /api/assistant/readiness` returns `200` with `{ "hybridReady": false, "retrievalMode": "lexical-warming" }` while warm-up is pending, `{ "hybridReady": true, "retrievalMode": "hybrid" }` after a non-empty catalog has been embedded successfully, and `{ "hybridReady": false, "retrievalMode": "lexical" }` when the catalog is empty or warm-up cannot produce vectors. It does not return cache paths or provider credentials. This endpoint is anonymous and uses only the global per-IP limit (100 requests/minute); the stricter `ai` policy applies to chat requests, so readiness polling does not consume chat quota.

### Guard response example

```json
{
  "answer": "Mình chỉ hỗ trợ tìm sản phẩm trong cửa hàng nên chưa trả lời được câu này. Bạn muốn tìm sản phẩm nào không?",
  "products": [],
  "sources": [],
  "retrieval": { "mode": "scope-guard", "hasEvidence": false },
  "evidenceStrength": "weak",
  "answerSource": "scope-guard",
  "intent": "out_of_scope"
}
```

Guard wording is currently configurable as constants and the greeting names “MiniShop”; the store name and support channel still require owner confirmation.

An insufficient-retrieval result is still `200`: `products` and `sources` are empty, `retrieval.hasEvidence` is false, and `evidenceStrength` is normally `weak` (explicit SKU intent remains `strong`). Provider failures normally use a backend-grounded fallback; the response does not expose provider error bodies.

## Errors

| Status | Body / condition |
|---|---|
| `400 Bad Request` | `{"error":"Query must contain 1 to 1000 characters."}`, `{"error":"Price filters are invalid."}`, `{"error":"Category and seller filters must be positive IDs."}`, `{"error":"Conversation context exceeds the allowed bounds."}`, or `{"error":"Visible product hints exceed the allowed limit."}`. |
| `413 Payload Too Large` | Request exceeds the 32,768-byte endpoint limit. The host may return an empty body. |
| `429 Too Many Requests` | Rate limiter rejection. Current middleware guarantees status only; no JSON error schema or `Retry-After` header is configured. |
| `500 Internal Server Error` | Unhandled exception: `{"error":"An unexpected error occurred."}`. Provider exceptions caught by assistant orchestration are returned as a grounded fallback instead. |

## OpenAPI caveats

The request-size limit and `429` policy are endpoint/runtime behavior and are not fully represented by generated Swagger response schemas. The API source declares response types for `200`, `400`, and `429`; this delta records observed controller and middleware behavior for `413` and `500` as well.
