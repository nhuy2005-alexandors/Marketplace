using ECommerce.Application.DTOs.AI;
using ECommerce.Application.DTOs.Catalog;
using ECommerce.Application.DTOs.Recommendations;
using ECommerce.Application.Interfaces;
using ECommerce.Infrastructure.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace ECommerce.Tests.Unit;

public sealed class AssistantServiceTests
{
    [Fact]
    public async Task ChatAsync_ReturnsNoEvidenceWithoutCallingGeneratorWhenRetrievalIsEmpty()
    {
        var search = new FakeSearch([]);
        var answer = new FakeAnswerProvider(null);
        var service = Service(search, answer, new FakeRecommendations());

        var result = await service.ChatAsync(new AssistantChatRequest("tìm tai nghe"), null);

        Assert.Equal("no-evidence", result.AnswerSource);
        Assert.Empty(result.Products);
        Assert.Empty(result.SourceProductIds);
        Assert.Equal(0, answer.CallCount);
    }

    [Fact]
    public async Task ChatAsync_ValidatesGeneratedSourceIdsAgainstBackendCandidates()
    {
        var first = Candidate(10, "Tai nghe Sony", 0.9);
        var second = Candidate(11, "Tai nghe khác", 0.8);
        var search = new FakeSearch([first, second]);
        var answer = new FakeAnswerProvider(new AssistantGeneratedAnswer("Sony phù hợp.", [10, 999]));
        var service = Service(search, answer, new FakeRecommendations());

        var result = await service.ChatAsync(new AssistantChatRequest("tìm tai nghe Sony"), null);

        Assert.Equal("fallback", result.AnswerSource);
        Assert.Equal([10, 11], result.SourceProductIds);
        Assert.Contains("Tai nghe Sony", result.Answer);
        Assert.DoesNotContain("Sony phù hợp.", result.Answer);
    }

    [Fact]
    public async Task ChatAsync_RejectsGeneratedAnswerWhenEverySourceIdIsOutsideBackendCandidates()
    {
        var search = new FakeSearch([Candidate(10, "Tai nghe Sony", 0.9)]);
        var answer = new FakeAnswerProvider(new AssistantGeneratedAnswer("Không có nguồn hợp lệ.", [999]));
        var service = Service(search, answer, new FakeRecommendations());

        var result = await service.ChatAsync(new AssistantChatRequest("tìm tai nghe Sony"), null);

        Assert.Equal("fallback", result.AnswerSource);
        Assert.Equal([10], result.SourceProductIds);
        Assert.Contains("Tai nghe Sony", result.Answer);
        Assert.Single(result.Products);
    }

    [Fact]
    public async Task ChatAsync_UsesGroundedFallbackWhenGeneratorOmitsSourceIds()
    {
        var search = new FakeSearch([Candidate(10, "Tai nghe Sony", 0.9)]);
        var answer = new FakeAnswerProvider(new AssistantGeneratedAnswer("Một câu trả lời không có nguồn.", []));
        var service = Service(search, answer, new FakeRecommendations());

        var result = await service.ChatAsync(new AssistantChatRequest("tìm tai nghe Sony"), null);

        Assert.Equal("fallback", result.AnswerSource);
        Assert.Contains("Tai nghe Sony", result.Answer);
        Assert.DoesNotContain("Một câu trả lời không có nguồn.", result.Answer);
        Assert.Equal([10], result.SourceProductIds);
    }

    [Fact]
    public async Task ChatAsync_UsesSharedRecommendationServiceForGenericSuggestions()
    {
        var recommendation = new FakeRecommendations();
        var search = new FakeSearch([]);
        var service = Service(search, new FakeAnswerProvider(null), recommendation);

        var result = await service.ChatAsync(
            new AssistantChatRequest("Gợi ý sản phẩm phù hợp cho tôi"), customerId: 42);

        Assert.Equal(42, recommendation.LastUserId);
        Assert.Equal("recommendation", result.Intent);
        Assert.Equal("recommendation", result.AnswerSource);
        Assert.Equal([21], result.SourceProductIds);
        Assert.Equal(0, search.CallCount);
    }

    [Fact]
    public async Task ChatAsync_UsesGroundedBackendFallbackWhenGeneratorIsUnavailable()
    {
        var search = new FakeSearch([Candidate(10, "Tai nghe Sony WH-1000", 0.85)]);
        var service = Service(search, new FakeAnswerProvider(null), new FakeRecommendations());

        var result = await service.ChatAsync(new AssistantChatRequest("tìm tai nghe Sony"), null);

        Assert.Equal("fallback", result.AnswerSource);
        Assert.Contains("Tai nghe Sony WH-1000", result.Answer);
        Assert.Contains("$89.00", result.Answer);
        Assert.Equal([10], result.SourceProductIds);
    }

    private static AssistantService Service(
        FakeSearch search,
        FakeAnswerProvider answer,
        FakeRecommendations recommendations) =>
        new(search, answer, recommendations, NullLogger<AssistantService>.Instance);

    private static AssistantRetrievalCandidate Candidate(int id, string name, double score)
    {
        var product = new ProductDto(
            id, name, "Catalog description", 89m, 5, null, 1, "Audio",
            2, "Approved Shop", 4.5, 12);
        return new AssistantRetrievalCandidate(product, score, score, 0, $"product:{id}", $"hash-{id}");
    }

    private sealed class FakeSearch(IReadOnlyList<AssistantRetrievalCandidate> candidates) : ISemanticSearchService
    {
        public bool IsHybridReady => false;
        public string RetrievalMode => "lexical";
        public int CallCount { get; private set; }

        public Task<IReadOnlyList<AssistantRetrievalCandidate>> SearchCandidatesAsync(
            string query, AssistantChatFilters? filters, int limit, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(candidates);
        }

        public Task WarmUpAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeAnswerProvider(AssistantGeneratedAnswer? answer) : IAssistantAnswerProvider
    {
        public int CallCount { get; private set; }

        public Task<AssistantGeneratedAnswer?> GenerateAsync(
            string query,
            IReadOnlyList<AssistantRetrievalCandidate> candidates,
            IReadOnlyList<AssistantConversationTurn> recentTurns,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(answer);
        }
    }

    private sealed class FakeRecommendations : IRecommendationService
    {
        public int? LastUserId { get; private set; }

        public Task<RecommendationResponseDto> GetForUserAsync(
            int? userId, int? take = null, CancellationToken cancellationToken = default)
        {
            LastUserId = userId;
            var product = new ProductDto(21, "Product recommendation", null, 20m, 2, null, 1,
                "Audio", 2, "Approved Shop", 0, 0);
            return Task.FromResult(new RecommendationResponseDto("personalized", DateTime.UtcNow,
                [new RecommendedProductDto(product, 0.8, [])]));
        }

        public Task<RecommendationResponseDto> GetSimilarAsync(
            int targetProductId, int? take = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(new RecommendationResponseDto("similar", DateTime.UtcNow, []));
    }
}
