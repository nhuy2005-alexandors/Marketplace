using ECommerce.Application.DTOs.AI;
using ECommerce.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace ECommerce.Infrastructure.AI;

public sealed class SemanticSearchService(
    IAppDbContext db,
    IEmbeddingProvider embeddings,
    EmbeddingCacheStore cache,
    ILogger<SemanticSearchService> logger) : ISemanticSearchService
{
    public bool IsHybridReady => false;
    public string RetrievalMode => "lexical";

    public Task<IReadOnlyList<AssistantRetrievalCandidate>> SearchCandidatesAsync(
        string query,
        AssistantChatFilters? filters,
        int limit,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<AssistantRetrievalCandidate>>([]);

    public Task WarmUpAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
