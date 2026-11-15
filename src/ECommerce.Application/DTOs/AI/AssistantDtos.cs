using ECommerce.Application.DTOs.Catalog;

namespace ECommerce.Application.DTOs.AI;

public sealed record AssistantChatRequest(
    string Query,
    AssistantChatFilters? Filters = null,
    IReadOnlyList<AssistantConversationTurn>? RecentTurns = null);

public sealed record AssistantChatFilters(
    int? CategoryId = null,
    int? BrandId = null,
    int? SellerId = null,
    decimal? MinPrice = null,
    decimal? MaxPrice = null,
    bool InStockOnly = true,
    IReadOnlyList<ProductAttributeFilter>? AttributeFilters = null);

public sealed record AssistantConversationTurn(string Role, string Content);

public sealed record AssistantChatResponse(
    string Answer,
    IReadOnlyList<ProductDto> Products,
    IReadOnlyList<int> SourceProductIds,
    string EvidenceStrength,
    string AnswerSource,
    string Intent,
    string RetrievalMode);

public sealed record AssistantRetrievalCandidate(
    ProductDto Product,
    double Score,
    double LexicalScore,
    double SemanticScore,
    string SourceId,
    string ContentHash);

public sealed record AssistantGeneratedAnswer(
    string Answer,
    IReadOnlyList<int> SourceProductIds);
