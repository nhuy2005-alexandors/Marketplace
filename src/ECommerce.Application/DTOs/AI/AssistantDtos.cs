using ECommerce.Application.DTOs.Catalog;

namespace ECommerce.Application.DTOs.AI;

public sealed record AssistantChatRequest(
    string? Query,
    AssistantChatFilters? Filters = null,
    AssistantConversationContext? Conversation = null,
    AssistantClientHints? Context = null);

public sealed record AssistantChatFilters(
    int? CategoryId = null,
    int? SellerId = null,
    decimal? MinPrice = null,
    decimal? MaxPrice = null,
    bool InStockOnly = false);

public sealed record AssistantConversationContext(
    string? ConversationId = null,
    IReadOnlyList<AssistantConversationTurn>? RecentTurns = null);

public sealed record AssistantConversationTurn(string Role, string Content);

// Browser-provided IDs are hints only. They never constrain or authorize retrieval.
public sealed record AssistantClientHints(IReadOnlyList<int>? VisibleProductIds = null);

public sealed record AssistantChatResponse(
    string Answer,
    IReadOnlyList<ProductDto> Products,
    IReadOnlyList<ProductSourceDto> Sources,
    RetrievalMetadataDto Retrieval,
    string? ConversationId = null);

public sealed record ProductSourceDto(string Type, int ProductId, string ChunkType, string ContentHash);

public sealed record RetrievalMetadataDto(string Mode, bool HasEvidence);

public sealed record AssistantRetrievalCandidate(
    ProductDto Product,
    double LexicalScore,
    double SemanticScore,
    double CombinedScore,
    IReadOnlyList<AssistantSourceChunk> Sources);

public sealed record AssistantSourceChunk(
    int ProductId,
    string Category,
    string? Brand,
    string ChunkType,
    string Content,
    string ContentHash,
    string EmbeddingModelVersion);

public sealed record AssistantGeneratedAnswer(string Answer, IReadOnlyList<int> SourceProductIds);
