using System.Globalization;
using ECommerce.Application.DTOs.AI;
using ECommerce.Application.DTOs.Recommendations;
using ECommerce.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace ECommerce.Infrastructure.AI;

public sealed class AssistantService : IAssistantService
{
    private const int ResultLimit = 8;
    private readonly ISemanticSearchService _search;
    private readonly IAssistantAnswerProvider _answerProvider;
    private readonly IRecommendationService _recommendations;
    private readonly ILogger<AssistantService> _logger;

    public AssistantService(
        ISemanticSearchService search,
        IAssistantAnswerProvider answerProvider,
        IRecommendationService recommendations,
        ILogger<AssistantService> logger)
    {
        _search = search;
        _answerProvider = answerProvider;
        _recommendations = recommendations;
        _logger = logger;
    }

    public async Task<AssistantChatResponse> ChatAsync(
        AssistantChatRequest request,
        int? customerId,
        CancellationToken cancellationToken = default)
    {
        var query = request.Query.Trim();
        var context = AssistantContextualizer.Contextualize(query, request.RecentTurns);

        if (IsGenericRecommendationRequest(query))
        {
            var response = await _recommendations.GetForUserAsync(customerId, ResultLimit, cancellationToken);
            if (response.Items.Count == 0)
                return NoEvidence("recommendation", "recommendation");

            var products = response.Items.Select(item => item.Product).ToArray();
            var sourceIds = products.Select(product => product.Id).Distinct().ToArray();
            return new AssistantChatResponse(
                RecommendationAnswer(response),
                products,
                sourceIds,
                "recommendation",
                "recommendation",
                "recommendation",
                "recommendation");
        }

        var candidates = await _search.SearchCandidatesAsync(
            context.Query,
            request.Filters,
            ResultLimit,
            cancellationToken);

        if (candidates.Count == 0)
            return NoEvidence("product_search", _search.RetrievalMode);

        var generated = await _answerProvider.GenerateAsync(
            context.Query,
            candidates,
            context.RecentTurns,
            cancellationToken);

        if (generated is not null)
        {
            var candidateIds = candidates.Select(candidate => candidate.Product.Id).ToHashSet();
            var validIds = generated.SourceProductIds
                .Where(candidateIds.Contains)
                .Distinct()
                .ToHashSet();

            // Model-supplied IDs are never authoritative. No valid backend source means abstain.
            if (validIds.Count == 0)
                return NoEvidence("product_search", _search.RetrievalMode);

            var grounded = candidates
                .Where(candidate => validIds.Contains(candidate.Product.Id))
                .ToArray();
            var answer = string.IsNullOrWhiteSpace(generated.Answer)
                ? BuildGroundedFallback(grounded)
                : generated.Answer.Trim();

            return BuildResponse(answer, grounded, "generated", "product_search");
        }

        // Provider disabled, unconfigured or unavailable: return only backend-grounded facts.
        _logger.LogDebug("Assistant generation unavailable; returning grounded catalog fallback.");
        return BuildResponse(
            BuildGroundedFallback(candidates),
            candidates,
            "fallback",
            "product_search");
    }

    private static AssistantChatResponse BuildResponse(
        string answer,
        IReadOnlyList<AssistantRetrievalCandidate> candidates,
        string answerSource,
        string intent)
    {
        var products = candidates.Select(candidate => candidate.Product).ToArray();
        var sourceIds = products.Select(product => product.Id).Distinct().ToArray();
        var evidenceStrength = candidates.Any(candidate => candidate.Score >= 0.55d) ? "strong" : "weak";

        return new AssistantChatResponse(
            answer,
            products,
            sourceIds,
            evidenceStrength,
            answerSource,
            intent,
            candidates.Any(candidate => candidate.SemanticScore > 0) ? "hybrid" : "lexical");
    }

    private static string BuildGroundedFallback(
        IReadOnlyList<AssistantRetrievalCandidate> candidates)
    {
        var facts = candidates.Take(4).Select(candidate =>
        {
            var product = candidate.Product;
            var price = product.Price.ToString("0.00", CultureInfo.InvariantCulture);
            return $"{product.Name} (${price}, tồn kho {product.Stock})";
        });
        return $"Các sản phẩm tìm thấy trong catalog: {string.Join("; ", facts)}.";
    }

    private static string RecommendationAnswer(RecommendationResponseDto response) =>
        response.Strategy switch
        {
            "personalized" => "Các gợi ý này dựa trên lịch sử mua hàng và đặc điểm sản phẩm của tài khoản.",
            "popular" => "Các sản phẩm này đã xuất hiện trong những đơn hàng thanh toán thành công.",
            "top_rated" => "Chưa đủ tín hiệu để cá nhân hóa; đây là các sản phẩm có đánh giá tốt.",
            "catalog_fallback" => "Chưa đủ dữ liệu mua hàng để cá nhân hóa; đây là một số sản phẩm đang có trong catalog.",
            _ => "Đây là các sản phẩm hiện có trong catalog."
        };

    private static bool IsGenericRecommendationRequest(string query)
    {
        var normalized = Normalize(query);
        return
            normalized.Contains("goi y san pham phu hop", StringComparison.Ordinal) ||
            normalized.Contains("goi y cho toi", StringComparison.Ordinal) ||
            normalized.Contains("goi y mon phu hop", StringComparison.Ordinal) ||
            normalized.Contains("recommend something", StringComparison.Ordinal) ||
            normalized.Contains("recommend for me", StringComparison.Ordinal) ||
            normalized.Contains("suggest something", StringComparison.Ordinal) ||
            normalized.Contains("what should i buy", StringComparison.Ordinal);
    }

    private static string Normalize(string value)
    {
        var decomposed = value.ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormD);
        var builder = new System.Text.StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(character) !=
                System.Globalization.UnicodeCategory.NonSpacingMark)
                builder.Append(character);
        }

        return builder.ToString()
            .Normalize(System.Text.NormalizationForm.FormC)
            .Replace('đ', 'd');
    }

    private static AssistantChatResponse NoEvidence(string intent, string retrievalMode) =>
        new(
            "Chưa tìm thấy sản phẩm có bằng chứng phù hợp trong catalog. Hãy thử điều chỉnh từ khóa hoặc bộ lọc.",
            [],
            [],
            "none",
            "no-evidence",
            intent,
            retrievalMode);
}
