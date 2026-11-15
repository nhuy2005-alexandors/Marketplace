using ECommerce.Application.DTOs.AI;
using ECommerce.Application.DTOs.Recommendations;
using ECommerce.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace ECommerce.Infrastructure.AI;

public sealed class AssistantService(
    ISemanticSearchService search,
    IAssistantAnswerProvider answerProvider,
    IRecommendationService recommendations,
    ILogger<AssistantService> logger) : IAssistantService
{
    public Task<AssistantChatResponse> ChatAsync(
        AssistantChatRequest request,
        int? customerId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new AssistantChatResponse(
            "Chưa tìm thấy thông tin phù hợp trong catalog.",
            [],
            [],
            "none",
            "no-evidence",
            "product_search",
            search.RetrievalMode));
}
