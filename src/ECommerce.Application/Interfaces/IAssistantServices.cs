using ECommerce.Application.DTOs.AI;

namespace ECommerce.Application.Interfaces;

public interface IAssistantService
{
    Task<AssistantChatResponse> ChatAsync(AssistantChatRequest request, int? customerId, CancellationToken ct = default);
}

public interface ISemanticSearchService
{
    Task<IReadOnlyList<AssistantRetrievalCandidate>> SearchCandidatesAsync(
        string query, AssistantChatFilters? filters, int limit, CancellationToken ct = default);
}

public interface IEmbeddingProvider
{
    string ModelVersion { get; }
    Task<IReadOnlyList<float[]>> GenerateEmbeddingsAsync(
        IReadOnlyList<string> texts, bool isQuery, CancellationToken ct = default);
}

public interface IAssistantAnswerProvider
{
    Task<AssistantGeneratedAnswer> GenerateAsync(
        string query,
        IReadOnlyList<AssistantRetrievalCandidate> candidates,
        IReadOnlyList<AssistantConversationTurn> recentTurns,
        CancellationToken ct = default);
}

// Shared Recommendation integration seam. The RAG lane does not implement or duplicate ranking.
public interface IRecommendationProvider
{
    Task<IReadOnlyList<ECommerce.Application.DTOs.Catalog.ProductDto>> GetProductsAsync(
        int? customerId, int limit, CancellationToken ct = default);
}
