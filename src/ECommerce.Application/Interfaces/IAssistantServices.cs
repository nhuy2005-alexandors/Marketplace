using ECommerce.Application.DTOs.AI;

namespace ECommerce.Application.Interfaces;

public interface IAssistantService
{
    Task<AssistantChatResponse> ChatAsync(
        AssistantChatRequest request,
        int? customerId,
        CancellationToken cancellationToken = default);
}

public interface ISemanticSearchService
{
    bool IsHybridReady { get; }
    string RetrievalMode { get; }

    Task<IReadOnlyList<AssistantRetrievalCandidate>> SearchCandidatesAsync(
        string query,
        AssistantChatFilters? filters,
        int limit,
        CancellationToken cancellationToken = default);

    Task WarmUpAsync(CancellationToken cancellationToken = default);
}

public interface IEmbeddingProvider
{
    Task<IReadOnlyList<float[]>?> EmbedAsync(
        IReadOnlyList<string> texts,
        string taskType,
        CancellationToken cancellationToken = default);
}

public interface IAssistantAnswerProvider
{
    Task<AssistantGeneratedAnswer?> GenerateAsync(
        string query,
        IReadOnlyList<AssistantRetrievalCandidate> candidates,
        IReadOnlyList<AssistantConversationTurn> recentTurns,
        CancellationToken cancellationToken = default);
}
