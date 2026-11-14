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
  "answerSource": "generated"
}
```

`evidenceStrength` is `strong` or `weak`. It is additive; the existing `retrieval`, product and source fields retain their previous meaning and ordering. A weak result is returned from the local Vietnamese response without calling Gemini generation; it keeps the same retrieved products and source IDs in the same order and says they are near the request but may not fully match. It avoids claiming that no product was found when suggestions are present. A strong result follows the existing generation path (or its existing grounded fallback).

`answerSource` is additive and identifies how `answer` was produced: `generated` for a valid Gemini response, `fallback` for a local recommendation/grounded response after a provider failure, timeout, open circuit, or when generation is disabled, and `weak-evidence` when retrieval evidence is weak and generation is skipped. Server configuration `Gemini:GenerationEnabled` defaults to `false`; with it disabled, strong-evidence requests use the local grounded response from the ranked candidates and set `answerSource="fallback"`. The answer-generation provider is not called. Embedding/retrieval configuration is independent and remains enabled as configured.

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
