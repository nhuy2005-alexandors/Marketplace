using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using ECommerce.Application.DTOs.AI;
using ECommerce.Application.DTOs.Catalog;
using ECommerce.Application.Interfaces;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ECommerce.Infrastructure.AI;

public sealed class SemanticSearchService : ISemanticSearchService
{
    private const int MaximumCatalogCandidates = 500;
    private const int MaximumAttributeFilters = 20;
    private static readonly HashSet<string> StopWords = new(StringComparer.Ordinal)
    {
        "a", "an", "and", "are", "can", "co", "cua", "cho", "gi", "hay", "is", "la",
        "mua", "mot", "nao", "please", "product", "san", "search", "show", "the", "tim",
        "toi", "with", "what", "you"
    };

    private static readonly Regex TokenPattern = new(@"[a-z0-9]+", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private readonly IAppDbContext _db;
    private readonly IEmbeddingProvider _embeddings;
    private readonly EmbeddingCacheStore _cache;
    private readonly ILogger<SemanticSearchService> _logger;

    public SemanticSearchService(
        IAppDbContext db,
        IEmbeddingProvider embeddings,
        EmbeddingCacheStore cache,
        ILogger<SemanticSearchService> logger)
    {
        _db = db;
        _embeddings = embeddings;
        _cache = cache;
        _logger = logger;
    }

    public bool IsHybridReady => _cache.Count > 0;
    public string RetrievalMode => IsHybridReady ? "hybrid" : "lexical";

    public async Task<IReadOnlyList<AssistantRetrievalCandidate>> SearchCandidatesAsync(
        string query,
        AssistantChatFilters? filters,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length > 1000 || !ValidFilters(filters))
            return [];

        var products = await LoadProductsAsync(filters, cancellationToken);
        if (products.Count == 0)
            return [];

        var documents = products.Select(product => CreateDocument(product)).ToArray();
        await EnsureProductEmbeddingsAsync(documents, cancellationToken);

        float[]? queryVector = null;
        var queryEmbeddings = await _embeddings.EmbedAsync([query.Trim()], "RETRIEVAL_QUERY", cancellationToken);
        if (queryEmbeddings is { Count: 1 })
            queryVector = queryEmbeddings[0];

        var queryTokens = Tokenize(query);
        if (queryTokens.Count == 0)
            return [];

        var ranked = new List<AssistantRetrievalCandidate>(documents.Length);
        foreach (var document in documents)
        {
            var lexicalScore = LexicalScore(queryTokens, Tokenize(document.Text));
            var semanticScore = 0d;

            if (queryVector is not null && _cache.TryGet(document.ContentHash, out var productVector))
                semanticScore = CosineSimilarity(queryVector, productVector);

            var hasSemantic = queryVector is not null && semanticScore > 0;
            var combinedScore = hasSemantic
                ? 0.65d * semanticScore + 0.35d * lexicalScore
                : lexicalScore;

            // A candidate needs actual lexical overlap or a meaningful semantic match.
            if (lexicalScore < 0.12d && semanticScore < 0.35d)
                continue;

            ranked.Add(new AssistantRetrievalCandidate(
                ToProductDto(document.Product),
                combinedScore,
                lexicalScore,
                semanticScore,
                $"product:{document.Product.Id}",
                document.ContentHash));
        }

        return ranked
            .OrderByDescending(candidate => candidate.Score)
            .ThenByDescending(candidate => candidate.Product.AverageRating)
            .ThenBy(candidate => candidate.Product.Id)
            .Take(Math.Clamp(limit, 1, 20))
            .ToArray();
    }

    public async Task WarmUpAsync(CancellationToken cancellationToken = default)
    {
        var products = await LoadProductsAsync(filters: null, cancellationToken);
        if (products.Count == 0)
            return;

        await EnsureProductEmbeddingsAsync(
            products.Select(CreateDocument).ToArray(),
            cancellationToken);

        _logger.LogInformation(
            "Semantic retrieval warm-up completed for {ProductCount} catalog products; cached embeddings: {EmbeddingCount}.",
            products.Count,
            _cache.Count);
    }

    private async Task<List<Product>> LoadProductsAsync(
        AssistantChatFilters? filters,
        CancellationToken ct)
    {
        var query = _db.Products.AsNoTracking()
            .Where(product => product.Seller.Role != UserRole.Seller ||
                              product.Seller.SellerStatus == SellerStatus.Approved)
            .Include(product => product.Category)
            .Include(product => product.Seller)
            .Include(product => product.Brand)
            .Include(product => product.Reviews)
            .Include(product => product.AttributeValues)
                .ThenInclude(value => value.AttributeDefinition)
            .AsQueryable();

        if (filters is not null)
        {
            if (filters.CategoryId is int categoryId)
                query = query.Where(product => product.CategoryId == categoryId);
            if (filters.BrandId is int brandId)
                query = query.Where(product => product.BrandId == brandId);
            if (filters.SellerId is int sellerId)
                query = query.Where(product => product.SellerId == sellerId);
            if (filters.MinPrice is decimal minPrice)
                query = query.Where(product => product.Price >= minPrice);
            if (filters.MaxPrice is decimal maxPrice)
                query = query.Where(product => product.Price <= maxPrice);
            if (filters.InStockOnly)
                query = query.Where(product => product.Stock > 0);

            foreach (var filter in filters.AttributeFilters ?? [])
            {
                var attributeId = filter.AttributeDefinitionId;
                if (filter.ValueText is not null)
                {
                    var value = filter.ValueText.Trim();
                    query = query.Where(product => product.AttributeValues.Any(attribute =>
                        attribute.AttributeDefinitionId == attributeId &&
                        attribute.AttributeDefinition.IsActive &&
                        attribute.AttributeDefinition.IsFilterable &&
                        attribute.ValueText == value));
                }
                else if (filter.MinValue.HasValue || filter.MaxValue.HasValue)
                {
                    var min = filter.MinValue;
                    var max = filter.MaxValue;
                    query = query.Where(product => product.AttributeValues.Any(attribute =>
                        attribute.AttributeDefinitionId == attributeId &&
                        attribute.AttributeDefinition.IsActive &&
                        attribute.AttributeDefinition.IsFilterable &&
                        attribute.ValueNumber.HasValue &&
                        (!min.HasValue || attribute.ValueNumber.Value >= min.Value) &&
                        (!max.HasValue || attribute.ValueNumber.Value <= max.Value)));
                }
                else if (filter.ValueBoolean.HasValue)
                {
                    var value = filter.ValueBoolean.Value;
                    query = query.Where(product => product.AttributeValues.Any(attribute =>
                        attribute.AttributeDefinitionId == attributeId &&
                        attribute.AttributeDefinition.IsActive &&
                        attribute.AttributeDefinition.IsFilterable &&
                        attribute.ValueBoolean == value));
                }
            }
        }
        else
        {
            query = query.Where(product => product.Stock > 0);
        }

        return await query
            .OrderBy(product => product.Id)
            .Take(MaximumCatalogCandidates)
            .ToListAsync(ct);
    }

    private async Task EnsureProductEmbeddingsAsync(
        IReadOnlyList<ProductDocument> documents,
        CancellationToken ct)
    {
        var missing = documents
            .Where(document => !_cache.TryGet(document.ContentHash, out _))
            .ToArray();

        if (missing.Length == 0)
            return;

        var vectors = await _embeddings.EmbedAsync(
            missing.Select(document => document.Text).ToArray(),
            "RETRIEVAL_DOCUMENT",
            ct);

        if (vectors is null || vectors.Count != missing.Length)
            return;

        for (var index = 0; index < missing.Length; index++)
            _cache.Set(missing[index].ContentHash, vectors[index]);
    }

    private static bool ValidFilters(AssistantChatFilters? filters)
    {
        if (filters is null)
            return true;
        if (filters.CategoryId is <= 0 || filters.BrandId is <= 0 || filters.SellerId is <= 0)
            return false;
        if (filters.MinPrice is < 0 || filters.MaxPrice is < 0 ||
            (filters.MinPrice is decimal min && filters.MaxPrice is decimal max && min > max))
            return false;

        var attributes = filters.AttributeFilters ?? [];
        if (attributes.Count > MaximumAttributeFilters ||
            attributes.Any(attribute => attribute.AttributeDefinitionId <= 0 ||
                (attribute.MinValue.HasValue && attribute.MaxValue.HasValue && attribute.MinValue > attribute.MaxValue)))
            return false;

        return attributes.All(attribute =>
        {
            var supplied = (attribute.ValueText is not null ? 1 : 0) +
                           (attribute.MinValue.HasValue || attribute.MaxValue.HasValue ? 1 : 0) +
                           (attribute.ValueBoolean.HasValue ? 1 : 0);
            return supplied == 1;
        });
    }

    private static ProductDocument CreateDocument(Product product)
    {
        var attributeText = product.AttributeValues
            .Where(value => value.AttributeDefinition is { IsActive: true })
            .Select(value => value.DataType switch
            {
                CatalogAttributeType.Text => $"{value.AttributeDefinition.DisplayName}: {value.ValueText}",
                CatalogAttributeType.Number => $"{value.AttributeDefinition.DisplayName}: {value.ValueNumber} {value.AttributeDefinition.Unit}",
                CatalogAttributeType.Boolean => $"{value.AttributeDefinition.DisplayName}: {(value.ValueBoolean == true ? "yes" : "no")}",
                _ => string.Empty
            });

        var text = string.Join(" ",
            product.Name,
            product.Description,
            product.Category.Name,
            product.Brand?.Name,
            product.Sku,
            string.Join(" ", attributeText));

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
        return new ProductDocument(product, text, hash);
    }

    private static ProductDto ToProductDto(Product product)
    {
        var reviews = product.Reviews;
        var reviewCount = reviews.Count;
        var averageRating = reviewCount > 0 ? Math.Round(reviews.Average(review => review.Rating), 2) : 0d;
        var attributes = product.AttributeValues
            .Where(value => value.AttributeDefinition is { IsActive: true })
            .OrderBy(value => value.AttributeDefinition.DisplayName)
            .Select(value => new ProductAttributeDto(
                value.AttributeDefinitionId,
                value.AttributeDefinition.Code,
                value.AttributeDefinition.DisplayName,
                value.DataType,
                value.AttributeDefinition.Unit,
                value.ValueText,
                value.ValueNumber,
                value.ValueBoolean))
            .ToArray();

        return new ProductDto(
            product.Id,
            product.Name,
            product.Description,
            product.Price,
            product.Stock,
            product.ImageUrl,
            product.CategoryId,
            product.Category.Name,
            product.SellerId,
            product.Seller.ShopName ?? product.Seller.FullName,
            averageRating,
            reviewCount,
            product.Sku,
            product.BrandId,
            product.Brand?.Name,
            attributes);
    }

    private static HashSet<string> Tokenize(string value)
    {
        var normalized = Normalize(value);
        return TokenPattern.Matches(normalized)
            .Select(match => match.Value)
            .Where(token => token.Length > 1 && !StopWords.Contains(token))
            .ToHashSet(StringComparer.Ordinal);
    }

    private static double LexicalScore(IReadOnlySet<string> queryTokens, IReadOnlySet<string> documentTokens)
    {
        if (queryTokens.Count == 0 || documentTokens.Count == 0)
            return 0;

        return (double)queryTokens.Count(documentTokens.Contains) / queryTokens.Count;
    }

    private static double CosineSimilarity(IReadOnlyList<float> left, IReadOnlyList<float> right)
    {
        if (left.Count == 0 || left.Count != right.Count)
            return 0;

        double dot = 0, leftNorm = 0, rightNorm = 0;
        for (var index = 0; index < left.Count; index++)
        {
            dot += left[index] * right[index];
            leftNorm += left[index] * left[index];
            rightNorm += right[index] * right[index];
        }

        if (leftNorm <= 0 || rightNorm <= 0)
            return 0;

        return Math.Clamp(dot / (Math.Sqrt(leftNorm) * Math.Sqrt(rightNorm)), 0d, 1d);
    }

    private static string Normalize(string value)
    {
        var decomposed = value.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(character) !=
                System.Globalization.UnicodeCategory.NonSpacingMark)
                builder.Append(character);
        }

        return builder.ToString()
            .Normalize(NormalizationForm.FormC)
            .Replace('đ', 'd');
    }

    private sealed record ProductDocument(Product Product, string Text, string ContentHash);
}
