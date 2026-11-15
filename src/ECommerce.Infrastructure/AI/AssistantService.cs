using ECommerce.Application.DTOs.AI;
using ECommerce.Application.DTOs.Catalog;
using ECommerce.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ECommerce.Infrastructure.AI;

public sealed class AssistantService : IAssistantService
{
    private readonly ISemanticSearchService _search;
    private readonly IAssistantAnswerProvider _answers;
    private readonly IRecommendationProvider _recommendations;
    private readonly ILogger<AssistantService> _logger;
    private readonly AssistantGenerationCircuitBreaker _generationCircuitBreaker;
    private readonly GeminiOptions _geminiOptions;
    private readonly AssistantDomainOptions _domainOptions;

    public AssistantService(
        ISemanticSearchService search,
        IAssistantAnswerProvider answers,
        IRecommendationProvider recommendations,
        ILogger<AssistantService> logger,
        AssistantGenerationCircuitBreaker? generationCircuitBreaker = null,
        IOptions<GeminiOptions>? geminiOptions = null,
        IOptions<AssistantDomainOptions>? domainOptions = null)
    {
        _search = search;
        _answers = answers;
        _recommendations = recommendations;
        _logger = logger;
        _geminiOptions = geminiOptions?.Value ?? new GeminiOptions();
        _domainOptions = domainOptions?.Value ?? new AssistantDomainOptions();
        _generationCircuitBreaker = generationCircuitBreaker ?? new AssistantGenerationCircuitBreaker(_geminiOptions);
    }

    public async Task<AssistantChatResponse> ChatAsync(AssistantChatRequest request, int? customerId, CancellationToken ct = default)
    {
        var query = request.Query!.Trim();
        var intent = AssistantIntentClassifier.Classify(query);
        var subjectiveQuestion = AssistantIntentClassifier.HasSubjectiveQuestion(query);
        var explicitSkuQuery = RetrievalScoring.TryFindSkuCandidate(query, out var requestedSku) &&
                               RetrievalScoring.HasNearbySkuCue(query, requestedSku);
        if (intent.Intent is "blocked" or "account_action" or "greeting")
            return ScopeGuard(intent.Intent, request.Conversation?.ConversationId);
        var filters = AssistantQueryFilters.Merge(query, request.Filters);
        if (filters.MinPrice.HasValue && filters.MaxPrice.HasValue && filters.MinPrice.Value > filters.MaxPrice.Value)
            return NoEvidence(request.Conversation?.ConversationId, filters: filters);

        var analysis = _geminiOptions.StrictEvidenceEnabled || _geminiOptions.ContextualizerV2Enabled
            ? await _search.AnalyzeQueryAsync(query, ct)
            : new AssistantQueryAnalysis([], true, false, []);
        var context = _geminiOptions.ContextualizerV2Enabled
            ? AssistantContextualizer.ContextualizeQueryV2(query, request.Conversation?.RecentTurns,
                analysis.HasNewProductTerm, analysis.Constraints.Count > 0, _domainOptions.FollowUpCues)
            : new AssistantContextualizer.ContextualizationResult(
                AssistantContextualizer.ContextualizeQuery(query, request.Conversation?.RecentTurns), "none");
        if (context.Action == "needs_clarification")
            return NoEvidence(request.Conversation?.ConversationId, contextAction: context.Action) with
            { Answer = "Bạn đang hỏi đến sản phẩm hoặc lựa chọn nào? Vui lòng nói rõ tên sản phẩm nhé." };
        var contextualQuery = context.Query;
        if (contextualQuery != query && (_geminiOptions.StrictEvidenceEnabled || _geminiOptions.ContextualizerV2Enabled))
            analysis = await _search.AnalyzeQueryAsync(contextualQuery, ct);
        var constraints = _geminiOptions.StrictEvidenceEnabled ? analysis.Constraints : Array.Empty<AssistantTypedConstraint>();
        var missingAttribute = analysis.UnsupportedConstraints.FirstOrDefault(value => value.StartsWith("context-attribute:", StringComparison.Ordinal));
        var unsupportedConstraint = analysis.UnsupportedConstraints.Any(value => !value.StartsWith("context-attribute:", StringComparison.Ordinal));
        var hasTypedIntent = constraints.Count > 0 || analysis.UnsupportedConstraints.Count > 0;
        if (intent.Intent == "out_of_scope" && !explicitSkuQuery && !hasTypedIntent &&
            !await _search.HasLexicalEvidenceAsync(intent.LexicalGateQuery, filters, ct))
            return ScopeGuard("out_of_scope", request.Conversation?.ConversationId);
        if (_geminiOptions.StrictEvidenceEnabled && unsupportedConstraint)
            return NoEvidence(request.Conversation?.ConversationId, constraints, context.Action, filters);

        if (intent.Intent == "product_search" && (IsRecommendationIntent(query) || IsSimilarRecommendationIntent(query)))
        {
            IReadOnlyList<ProductDto> recommendationProducts = [];
            var isSimilarRequest = IsSimilarRecommendationIntent(query);
            var shouldConstrainRecommendation = analysis.HasCatalogTerm || hasTypedIntent || HasExplicitSearchFilters(filters);
            if (isSimilarRequest)
            {
                if (shouldConstrainRecommendation)
                {
                    var targetCandidates = await _search.SearchCandidatesAsync(contextualQuery, filters, constraints, 1, ct);
                    if (targetCandidates.Count > 0)
                        recommendationProducts = await _recommendations.GetSimilarProductsAsync(targetCandidates[0].Product.Id, 4, ct);
                    if (recommendationProducts.Count > 0)
                    {
                        var matchingCandidates = await _search.SearchCandidatesAsync(contextualQuery, filters, constraints, 8, ct);
                        var matchingIds = matchingCandidates.Select(candidate => candidate.Product.Id).ToHashSet();
                        recommendationProducts = recommendationProducts.Where(product => matchingIds.Contains(product.Id)).Take(4).ToArray();
                    }
                }
            }
            else
            {
                recommendationProducts = await _recommendations.GetProductsAsync(customerId, 100, ct);
                if (shouldConstrainRecommendation)
                {
                    var matchingCandidates = await _search.SearchCandidatesAsync(contextualQuery, filters, constraints, 8, ct);
                    var matchingIds = matchingCandidates.Select(candidate => candidate.Product.Id).ToHashSet();
                    recommendationProducts = recommendationProducts.Where(product => matchingIds.Contains(product.Id)).Take(4).ToArray();
                }
                else
                {
                    recommendationProducts = recommendationProducts.Take(4).ToArray();
                }
            }

            if (recommendationProducts.Count > 0)
            {
                var recommendationSources = recommendationProducts.Select(product =>
                    new ProductSourceDto("recommendation", product.Id, "recommendation", string.Empty)).ToArray();
                var recommendationAnswer = isSimilarRequest
                    ? "Đây là một số sản phẩm tương tự được hệ thống đề xuất."
                    : "Đây là một số sản phẩm được Recommendation Engine đề xuất.";
                return new AssistantChatResponse(recommendationAnswer, recommendationProducts, recommendationSources,
                    new RetrievalMetadataDto("recommendation", true), request.Conversation?.ConversationId ?? Guid.NewGuid().ToString("N"),
                    "strong", "fallback", "product_search", "recommendation", ToConstraintDtos(constraints, filters), context.Action);
            }
        }

        if (_geminiOptions.StrictEvidenceEnabled && constraints.Count == 0 && !analysis.HasCatalogTerm && !explicitSkuQuery)
            return NoEvidence(request.Conversation?.ConversationId, constraints, context.Action, filters);

        var searchFilters = contextualQuery != query ? AssistantQueryFilters.Merge(contextualQuery, filters) : filters;
        var candidates = await _search.SearchCandidatesAsync(contextualQuery, searchFilters, constraints, 4, ct);
        if (candidates.Count == 0 && contextualQuery != query)
        {
            candidates = await _search.SearchCandidatesAsync(query, filters, constraints, 4, ct);
        }

        if (_geminiOptions.StrictEvidenceEnabled && missingAttribute is not null)
        {
            var field = missingAttribute["context-attribute:".Length..];
            if (candidates.Count == 0) return NoAttributeInfo(request.Conversation?.ConversationId, field, context.Action, constraints, filters);
            var infoProducts = candidates.Select(candidate => candidate.Product).ToArray();
            var infoSources = candidates.SelectMany(candidate => candidate.Sources.Select(source =>
                new ProductSourceDto("product", candidate.Product.Id, source.ChunkType, source.ContentHash))).Distinct().ToArray();
            var infoStrength = candidates.Any(candidate => candidate.LexicalScore > 0) ? "strong" : "weak";
            return new AssistantChatResponse(NoAttributeInfoAnswer(field), infoProducts, infoSources,
                new RetrievalMetadataDto(_search.RetrievalMode, true), request.Conversation?.ConversationId ?? Guid.NewGuid().ToString("N"), infoStrength,
                "fallback", "product_search", _search.RetrievalMode, ToConstraintDtos(constraints, filters), context.Action);
        }

        var skuBranch = explicitSkuQuery || IsExplicitSkuBranch(query, candidates) || (contextualQuery != query && IsExplicitSkuBranch(contextualQuery, candidates));
        var evidenceStrength = skuBranch || candidates.Any(candidate => candidate.LexicalScore > 0) ? "strong" : "weak";
        if (candidates.Count == 0)
        {
            if (_geminiOptions.StrictEvidenceEnabled)
                return skuBranch
                    ? NoSkuMatch(request.Conversation?.ConversationId, constraints, context.Action, filters)
                    : NoEvidence(request.Conversation?.ConversationId, constraints, context.Action, filters);
            var noEvidence = LegacyNoEvidence(request.Conversation?.ConversationId, evidenceStrength,
                skuBranch ? "fallback" : "weak-evidence");
            return subjectiveQuestion ? noEvidence with { Answer = noEvidence.Answer + "\n\n" + AssistantIntentClassifier.SubjectiveCaveat } : noEvidence;
        }

        AssistantGeneratedAnswer? generated = null;
        var halfOpenProbe = false;
        var generationAllowed = evidenceStrength == "strong" && !subjectiveQuestion && _geminiOptions.GenerationEnabled && _generationCircuitBreaker.TryAcquire(out halfOpenProbe);
        var answerSource = evidenceStrength == "weak" ? "weak-evidence" : "fallback";
        if (generationAllowed)
        {
            try
            {
                var turns = AssistantContextualizer.TakeLastThreeConversationPairs(
                    context.Action == "new_topic" ? null : request.Conversation?.RecentTurns).Where(turn =>
                        (turn.Role.Equals("user", StringComparison.OrdinalIgnoreCase) || turn.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase)) &&
                        !string.IsNullOrWhiteSpace(turn.Content))
                    .Select(turn => turn with { Content = turn.Content.Trim()[..Math.Min(turn.Content.Trim().Length, 500)] }).ToArray();
                generated = await _answers.GenerateAsync(query, candidates, turns, ct);
                _generationCircuitBreaker.RecordSuccess();
                answerSource = "generated";
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
        else if (evidenceStrength == "strong" && subjectiveQuestion)
            _logger.LogInformation("Assistant generation skipped for a subjective product-impression question; returning catalog-grounded wording.");
        else if (evidenceStrength == "strong" && !_geminiOptions.GenerationEnabled)
            _logger.LogInformation("Assistant generation is disabled by Gemini:GenerationEnabled; returning a grounded local response.");
        else if (evidenceStrength == "strong")
            _logger.LogInformation("Assistant generation skipped because its in-memory circuit is open.");

        var allowedIds = candidates.Select(candidate => candidate.Product.Id).ToHashSet();
        var sourceIds = generated?.SourceProductIds.Where(allowedIds.Contains).Distinct().ToHashSet() ?? [];
        if (generated is not null && generated.SourceProductIds.Count == 0)
            return LegacyNoEvidence(request.Conversation?.ConversationId, evidenceStrength, "fallback");
        var hasValidGeneratedSources = generated is not null && sourceIds.Count > 0 && !string.IsNullOrWhiteSpace(generated.Answer);
        if (generated is not null && !hasValidGeneratedSources) answerSource = "fallback";
        var selected = hasValidGeneratedSources
            ? candidates.Where(candidate => sourceIds.Contains(candidate.Product.Id)).ToArray()
            : candidates.ToArray();
        var sourcesOut = selected.SelectMany(candidate => candidate.Sources.Select(source =>
            new ProductSourceDto("product", candidate.Product.Id, source.ChunkType, source.ContentHash))).Distinct().ToArray();
        var answer = hasValidGeneratedSources ? generated!.Answer.Trim()
            : evidenceStrength == "weak" ? BuildWeakEvidenceAnswer(selected.Select(candidate => candidate.Product).ToArray())
            : BuildLocalAnswer(query, selected.Select(candidate => candidate.Product).ToArray());
        if (subjectiveQuestion) answer += "\n\n" + AssistantIntentClassifier.SubjectiveCaveat;
        if (evidenceStrength == "weak") answerSource = "weak-evidence";
        var retrievalMode = _search.RetrievalMode;
        return new AssistantChatResponse(answer, selected.Select(candidate => candidate.Product).ToArray(), sourcesOut,
            new RetrievalMetadataDto(retrievalMode, true), request.Conversation?.ConversationId ?? Guid.NewGuid().ToString("N"), evidenceStrength, answerSource, "product_search",
            retrievalMode, ToConstraintDtos(constraints, filters), context.Action);
    }

    private static AssistantChatResponse ScopeGuard(string intent, string? conversationId)
    {
        var answer = intent switch
        {
            "blocked" => AssistantIntentClassifier.BlockedAnswer,
            "account_action" => AssistantIntentClassifier.AccountAnswer,
            "greeting" => AssistantIntentClassifier.GreetingAnswer,
            _ => AssistantIntentClassifier.OutOfScopeAnswer
        };
        return new AssistantChatResponse(answer, [], [], new RetrievalMetadataDto("scope-guard", false),
            conversationId ?? Guid.NewGuid().ToString("N"), "weak", "scope-guard", intent);
    }

    private static bool IsRecommendationIntent(string query) =>
        new[] { "goi y", "de xuat", "tu van", "recommend", "suggest", "what should i buy", "nen mua gi" }
            .Any(phrase => ContainsNormalizedPhrase(query, phrase));

    private static bool IsSimilarRecommendationIntent(string query) =>
        new[] { "similar", "tuong tu", "giong san pham", "san pham giong", "loai giong" }
            .Any(phrase => ContainsNormalizedPhrase(query, phrase));

    private static bool ContainsNormalizedPhrase(string query, string phrase)
    {
        var normalizedQuery = RetrievalScoring.Normalize(query);
        var normalizedPhrase = RetrievalScoring.Normalize(phrase);
        return normalizedQuery.Contains(normalizedPhrase, StringComparison.Ordinal);
    }

    private static bool HasExplicitSearchFilters(AssistantChatFilters filters) =>
        filters.CategoryId is not null || filters.SellerId is not null || filters.MinPrice is not null ||
        filters.MaxPrice is not null || filters.InStockOnly;

    private AssistantChatResponse NoEvidence(string? conversationId, IReadOnlyList<AssistantTypedConstraint>? constraints = null,
        string contextAction = "none", AssistantChatFilters? filters = null) => new(
        "Mình không tìm thấy sản phẩm nào đáp ứng các điều kiện bạn đưa ra.",
        [], [], new RetrievalMetadataDto(_search.RetrievalMode, false), conversationId ?? Guid.NewGuid().ToString("N"), "none", "no-evidence", "product_search",
        _search.RetrievalMode, ToConstraintDtos(constraints ?? [], filters), contextAction);

    private AssistantChatResponse NoSkuMatch(string? conversationId, IReadOnlyList<AssistantTypedConstraint> constraints,
        string contextAction, AssistantChatFilters? filters) => new(
        "Mình không tìm thấy sản phẩm có mã SKU này trong cửa hàng.", [], [], new RetrievalMetadataDto(_search.RetrievalMode, false),
        conversationId ?? Guid.NewGuid().ToString("N"), "strong", "fallback", "product_search", _search.RetrievalMode,
        ToConstraintDtos(constraints, filters), contextAction);

    private AssistantChatResponse LegacyNoEvidence(string? conversationId, string evidenceStrength, string answerSource) => new(
        "Mình chưa chắc có sản phẩm khớp hoàn toàn với yêu cầu. Bạn thử điều chỉnh từ khóa hoặc bộ lọc nhé.",
        [], [], new RetrievalMetadataDto(_search.RetrievalMode, false), conversationId ?? Guid.NewGuid().ToString("N"), evidenceStrength, answerSource,
        RetrievalMode: _search.RetrievalMode);

    private AssistantChatResponse NoAttributeInfo(string? conversationId, string field, string contextAction,
        IReadOnlyList<AssistantTypedConstraint> constraints, AssistantChatFilters? filters) => new(
        NoAttributeInfoAnswer(field), [], [], new RetrievalMetadataDto(_search.RetrievalMode, false),
        conversationId ?? Guid.NewGuid().ToString("N"), "none", "no-evidence", "product_search", _search.RetrievalMode, ToConstraintDtos(constraints, filters), contextAction);

    private static string NoAttributeInfoAnswer(string field) =>
        $"Mình không có thông tin có cấu trúc về {field} trong dữ liệu của cửa hàng nên không thể xác nhận.";

    private static IReadOnlyList<AssistantConstraintDto> ToConstraintDtos(IReadOnlyList<AssistantTypedConstraint> constraints,
        AssistantChatFilters? filters = null) =>
        ToConstraintDtosCore(constraints, filters);

    private static IReadOnlyList<AssistantConstraintDto> ToConstraintDtosCore(IReadOnlyList<AssistantTypedConstraint> constraints,
        AssistantChatFilters? filters)
    {
        var reported = constraints.Select(constraint => new AssistantConstraintDto(constraint.Field, constraint.Operator,
            constraint.ValueText ?? constraint.ValueBoolean?.ToString() ?? constraint.ValueNumber?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
            constraint.Unit)).ToList();
        if (filters is null) return reported;
        if (filters.MinPrice is decimal min) reported.Add(new AssistantConstraintDto("price", "greaterThanOrEqual", min.ToString(System.Globalization.CultureInfo.InvariantCulture), "currency"));
        if (filters.MaxPrice is decimal max) reported.Add(new AssistantConstraintDto("price", "lessThanOrEqual", max.ToString(System.Globalization.CultureInfo.InvariantCulture), "currency"));
        if (filters.InStockOnly) reported.Add(new AssistantConstraintDto("stock", "greaterThan", "0", "items"));
        if (filters.CategoryId is int category) reported.Add(new AssistantConstraintDto("categoryId", "equals", category.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        if (filters.SellerId is int seller) reported.Add(new AssistantConstraintDto("sellerId", "equals", seller.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        return reported;
    }

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
        $"Mình tìm được vài sản phẩm gần với yêu cầu của bạn, nhưng chưa chắc khớp hoàn toàn. Bạn xem thử nhé:\n{BuildProductLines(products)}\nGiá và tồn kho được hiển thị từ dữ liệu sản phẩm hiện tại.";

    private static string BuildProductLines(IReadOnlyList<ProductDto> products) =>
        string.Join("\n", products.Select(product => $"• {product.Name} — {product.CategoryName}"));
}
