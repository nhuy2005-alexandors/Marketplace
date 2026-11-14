# Recommendation contract proposal for Khánh

**Status:** proposal only; no Recommendation code or shared DTO changed.

1. **Shared product shape:** return the existing `ECommerce.Application.DTOs.Catalog.ProductDto` unchanged: `Id`, `Name`, `Description`, `Price`, `Stock`, `ImageUrl`, `CategoryId`, `CategoryName`, `SellerId`, `SellerShopName`, `AverageRating`, and `ReviewCount`. RAG source metadata stays wrapped separately; it must not create an alternate product DTO.
2. **Service ownership/location:** Khánh's Recommendation lane owns ranking and its Application service/interface. Register the implementation in Infrastructure and expose it to RAG through the existing `IRecommendationProvider` adapter/port. RAG may request a bounded number of results, but must not duplicate or tune the scoring algorithm.
3. **Identity:** obtain customer ID from authenticated server context, never from request JSON. The port's nullable `customerId` is the only guest signal; keep profile/history data inside the Recommendation service and do not send it to Gemini.
4. **Anonymous behavior:** if Khánh provides an explicitly public, non-personalized popular/trending mode, the adapter may use that for a guest. Otherwise return an empty recommendation list and let the assistant continue with catalog retrieval or a text-only no-result response. Never substitute another customer's profile or history.
5. **Fresh commerce fields:** populate price, stock, rating and seller display data from current backend DTO/catalog state. RAG uses only the product IDs and opaque retrieval evidence returned by its own candidate search.
6. **Acceptance handshake:** Khánh confirms the owned service/interface and guest policy; both lanes agree that the adapter returns `IReadOnlyList<ProductDto>` and that integration is validated on the same commit. This proposal does not authorize a shared DTO/API change.

## RAG response addition — evidence strength

The RAG lane adds `evidenceStrength: "strong" | "weak"` to `AssistantChatResponse`. This is an additive response field owned by the RAG lane; it does not change the shared `ProductDto`, recommendation service contract, product order, or source IDs. Weak lexical evidence uses a local answer and skips Gemini generation. Recommendation behavior and the Recommendation lane's ranking remain unchanged.

The RAG lane also adds additive `answerSource: "generated" | "fallback" | "weak-evidence"` to identify generated text, the existing local fallback after provider trouble/open circuit, or the fixed answer used when evidence is weak. Khánh's lane should treat it as display/diagnostic metadata and retain unknown-value compatibility.
