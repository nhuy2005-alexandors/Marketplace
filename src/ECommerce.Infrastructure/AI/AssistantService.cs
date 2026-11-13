using ECommerce.Application.DTOs.AI;
using ECommerce.Application.DTOs.Catalog;
using ECommerce.Application.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace ECommerce.Infrastructure.AI;

public sealed class AssistantService : IAssistantService
{
    private static readonly Regex PromptExtractionRequest = new(
        @"(?i)(?:\b(?:reveal|show|print|tell\s+me|repeat|disclose|what\s+is|give\s+me|tiết\s+lộ|cho\s+biết|in\s+ra|đưa\s+tôi)\b.{0,48}\b(?:api[\s-]*key|system[\s-]*(?:prompt|instruction)|secret|bí\s+mật|khóa\s+api)\b|\b(?:api[\s-]*key|system[\s-]*(?:prompt|instruction)|secret|bí\s+mật|khóa\s+api)\b.{0,48}\b(?:reveal|show|print|repeat|disclose|tiết\s+lộ|in\s+ra)\b)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private readonly ISemanticSearchService _search;
    private readonly IAssistantAnswerProvider _answers;
    private readonly IRecommendationProvider _recommendations;
    private readonly ILogger<AssistantService> _logger;
    private readonly AssistantGenerationCircuitBreaker _generationCircuitBreaker;

    public AssistantService(
        ISemanticSearchService search,
        IAssistantAnswerProvider answers,
        IRecommendationProvider recommendations,
        ILogger<AssistantService> logger,
        AssistantGenerationCircuitBreaker? generationCircuitBreaker = null)
    {
        _search = search;
        _answers = answers;
        _recommendations = recommendations;
        _logger = logger;
        _generationCircuitBreaker = generationCircuitBreaker ?? new AssistantGenerationCircuitBreaker(new GeminiOptions());
    }

    public async Task<AssistantChatResponse> ChatAsync(AssistantChatRequest request, int? customerId, CancellationToken ct = default)
    {
        var query = request.Query!.Trim();
        if (PromptExtractionRequest.IsMatch(query)) return NoEvidence(request.Conversation?.ConversationId);
        var filters = AssistantQueryFilters.Merge(query, request.Filters);
        if (filters.MinPrice.HasValue && filters.MaxPrice.HasValue && filters.MinPrice.Value > filters.MaxPrice.Value)
            return NoEvidence(request.Conversation?.ConversationId);
        var candidates = await _search.SearchCandidatesAsync(query, filters, 4, ct);
        if (candidates.Count == 0 && IsRecommendationIntent(query))
        {
            var products = await _recommendations.GetProductsAsync(customerId, 4, ct);
            if (products.Count > 0)
            {
                var sources = products.Select(product => new ProductSourceDto("recommendation", product.Id, "recommendation", string.Empty)).ToArray();
                return new AssistantChatResponse("Đây là các sản phẩm được hệ thống gợi ý cho bạn.", products, sources,
                    new RetrievalMetadataDto("recommendation", true), request.Conversation?.ConversationId ?? Guid.NewGuid().ToString("N"), "strong");
            }
        }
        var skuBranch = IsExplicitSkuBranch(query, candidates);
        var evidenceStrength = skuBranch || candidates.Any(candidate => candidate.LexicalScore > 0) ? "strong" : "weak";
        if (candidates.Count == 0)
            return NoEvidence(request.Conversation?.ConversationId, evidenceStrength);

        AssistantGeneratedAnswer? generated = null;
        var halfOpenProbe = false;
        var generationAllowed = evidenceStrength == "strong" && _generationCircuitBreaker.TryAcquire(out halfOpenProbe);
        if (generationAllowed)
        {
            try
            {
                var turns = (request.Conversation?.RecentTurns ?? []).Where(turn =>
                        (turn.Role.Equals("user", StringComparison.OrdinalIgnoreCase) || turn.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase)) &&
                        !string.IsNullOrWhiteSpace(turn.Content))
                    .TakeLast(4).Select(turn => turn with { Content = turn.Content.Trim()[..Math.Min(turn.Content.Trim().Length, 500)] }).ToArray();
                generated = await _answers.GenerateAsync(query, candidates, turns, ct);
                _generationCircuitBreaker.RecordSuccess();
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                _generationCircuitBreaker.AbandonHalfOpenProbe(halfOpenProbe);
                throw;
            }
            catch (Exception ex)
            {
                _generationCircuitBreaker.RecordFailure(halfOpenProbe);
                _logger.LogWarning("Assistant generation failed; returning a grounded local response. Failure type: {FailureType}", ex.GetType().Name);
            }
        }
        else if (evidenceStrength == "strong")
            _logger.LogInformation("Assistant generation skipped because its in-memory circuit is open.");

        var allowedIds = candidates.Select(candidate => candidate.Product.Id).ToHashSet();
        var sourceIds = generated?.SourceProductIds.Where(allowedIds.Contains).Distinct().ToHashSet() ?? [];
        if (generated is not null && generated.SourceProductIds.Count == 0)
            return NoEvidence(request.Conversation?.ConversationId);
        var hasValidGeneratedSources = generated is not null && sourceIds.Count > 0 && !string.IsNullOrWhiteSpace(generated.Answer);
        var selected = hasValidGeneratedSources
            ? candidates.Where(candidate => sourceIds.Contains(candidate.Product.Id)).ToArray()
            : candidates.ToArray();
        var sourcesOut = selected.SelectMany(candidate => candidate.Sources.Select(source =>
            new ProductSourceDto("product", candidate.Product.Id, source.ChunkType, source.ContentHash))).Distinct().ToArray();
        var answer = hasValidGeneratedSources ? generated!.Answer.Trim()
            : evidenceStrength == "weak" ? BuildWeakEvidenceAnswer(selected.Select(candidate => candidate.Product).ToArray())
            : BuildLocalAnswer(query, selected.Select(candidate => candidate.Product).ToArray());
        return new AssistantChatResponse(answer, selected.Select(candidate => candidate.Product).ToArray(), sourcesOut,
            new RetrievalMetadataDto("hybrid", true), request.Conversation?.ConversationId ?? Guid.NewGuid().ToString("N"), evidenceStrength);
    }

    private static bool IsRecommendationIntent(string query) =>
        query.Contains("gợi ý", StringComparison.OrdinalIgnoreCase) ||
        query.Contains("đề xuất", StringComparison.OrdinalIgnoreCase) ||
        query.Contains("recommend", StringComparison.OrdinalIgnoreCase);

    private static AssistantChatResponse NoEvidence(string? conversationId, string evidenceStrength = "weak") => new(
        "Mình chưa tìm thấy sản phẩm có đủ thông tin phù hợp trong catalog. Bạn thử đổi từ khóa hoặc bộ lọc nhé.",
        [], [], new RetrievalMetadataDto("hybrid", false), conversationId ?? Guid.NewGuid().ToString("N"), evidenceStrength);

    private static bool IsExplicitSkuBranch(string query, IReadOnlyList<AssistantRetrievalCandidate> candidates)
    {
        if (RetrievalScoring.TryFindSkuCandidate(query, out var sku) && RetrievalScoring.HasNearbySkuCue(query, sku)) return true;
        var chunks = candidates.SelectMany(candidate => candidate.Sources).Select(source => new RetrievalScoringChunk
        {
            ProductId = source.ProductId,
            Type = source.ChunkType,
            Content = source.Content
        }).ToArray();
        return RetrievalScoring.TryResolveSkuIntent(query, chunks, out _);
    }

    private static string BuildLocalAnswer(string query, IReadOnlyList<ProductDto> products)
    {
        return $"Các sản phẩm phù hợp với “{query}” trong catalog:\n{BuildProductLines(products)}\nGiá và tồn kho được hiển thị từ dữ liệu sản phẩm hiện tại.";
    }

    private static string BuildWeakEvidenceAnswer(IReadOnlyList<ProductDto> products) =>
        $"Mình chưa tìm thấy sản phẩm khớp chắc chắn. Đây chỉ là gợi ý gần nhất:\n{BuildProductLines(products)}\nGiá và tồn kho được hiển thị từ dữ liệu sản phẩm hiện tại.";

    private static string BuildProductLines(IReadOnlyList<ProductDto> products) =>
        string.Join("\n", products.Select(product => $"• {product.Name} — {product.CategoryName}"));
}
