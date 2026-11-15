using ECommerce.Application.DTOs.AI;
using ECommerce.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ECommerce.Infrastructure.AI;

public sealed class GeminiAssistantProvider(
    HttpClient http,
    IOptions<GeminiOptions> options,
    ILogger<GeminiAssistantProvider> logger) : IEmbeddingProvider, IAssistantAnswerProvider
{
    public Task<IReadOnlyList<float[]>?> EmbedAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<float[]>?>(null);

    public Task<AssistantGeneratedAnswer?> GenerateAsync(
        string query,
        IReadOnlyList<AssistantRetrievalCandidate> candidates,
        IReadOnlyList<AssistantConversationTurn> recentTurns,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<AssistantGeneratedAnswer?>(null);
}
