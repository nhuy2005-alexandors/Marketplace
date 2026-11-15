# Recommendation contract for Khang and Khánh

**Status:** implemented on the integration branch; response/product DTOs remain unchanged.

1. **Shared product shape:** return the existing `ECommerce.Application.DTOs.Catalog.ProductDto` unchanged: `Id`, `Name`, `Description`, `Price`, `Stock`, `ImageUrl`, `CategoryId`, `CategoryName`, `SellerId`, `SellerShopName`, `AverageRating`, and `ReviewCount`. RAG source metadata stays wrapped separately; it must not create an alternate product DTO.
2. **Service ownership/location:** the Recommendation lane owns ranking in `IRecommendationService`; `RecommendationsController` and RAG's `RecommendationProviderAdapter` both call that same scoped Application service. `NullRecommendationProvider` is removed. RAG requests bounded results and does not duplicate or tune the scoring algorithm.
3. **Identity:** obtain customer ID from authenticated server context, never from request JSON. The port's nullable `customerId` is the only guest signal; keep profile/history data inside the Recommendation service and do not send it to Gemini.
4. **Anonymous behavior:** the shared service uses its existing popularity and then top-rated fallback for guests and customers without eligible purchase history. It never substitutes another customer's profile or history.
5. **Fresh commerce fields:** populate price, stock, rating and seller display data from current backend DTO/catalog state. RAG uses only the product IDs and opaque retrieval evidence returned by its own candidate search.
6. **RAG routing:** advice/recommendation requests use the shared service. If the query names a catalog product or includes ordinary filters, RAG intersects the Recommendation-ranked IDs with its filtered catalog candidates before returning them. Similar-product requests first resolve a target through RAG retrieval, then use `GetSimilarAsync`; ordinary product-information queries continue through the existing RAG retrieval path.
7. **Acceptance handshake:** Khang and Khánh review the common integration commit. Recommendation results use the existing `ProductDto` and additive source metadata (`type=recommendation`); no product DTO/API schema change is required. Customer ID comes from authenticated server context.

## RAG response addition — evidence strength

The RAG lane adds `evidenceStrength: "strong" | "weak" | "none"` to `AssistantChatResponse`. This is an additive response field owned by the RAG lane; it does not change the shared `ProductDto` or Recommendation API contract. Weak lexical evidence uses a local answer and skips Gemini generation. Requests classified as recommendation intent use the shared ranking service described above; ordinary retrieval ranking remains unchanged.

The RAG lane also adds additive `answerSource: "generated" | "fallback" | "weak-evidence" | "scope-guard"` to identify generated text, the existing local fallback after provider trouble/open circuit, weak-evidence wording, or a fixed non-product scope response. Khánh's lane should treat it as display/diagnostic metadata and retain unknown-value compatibility.

The RAG lane adds additive `intent: "product_search" | "greeting" | "account_action" | "out_of_scope" | "blocked"`. This field belongs to the RAG lane only; it is not a change to `ProductDto` or the Recommendation API. Khánh's client should tolerate a missing or unknown value. For a recognized non-product intent, show the `answer` text and do not render product cards because `products` and `sources` are empty; the scope guard must not be used to rank or filter Recommendation results. Recommendation intent routing is implemented as documented above and uses the common service; ordinary RAG product-information behavior is preserved.
