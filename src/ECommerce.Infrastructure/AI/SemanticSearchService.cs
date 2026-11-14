using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using ECommerce.Application.DTOs.AI;
using ECommerce.Application.DTOs.Catalog;
using ECommerce.Application.Interfaces;
using ECommerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ECommerce.Infrastructure.AI;

public sealed class SemanticSearchService : ISemanticSearchService
{
    private const int MaxCatalogCandidates = 1000;
    private const int EmbeddingBatchSize = 16;
    private static readonly ConcurrentDictionary<string, float[]> EmbeddingCache = new(StringComparer.Ordinal);
    private static readonly SemaphoreSlim EmbeddingCachePopulationGate = new(1, 1);
    private static bool EmbeddingCacheLoadedFromFile;

    private readonly IAppDbContext _db;
    private readonly IEmbeddingProvider _embeddings;
    private readonly IEmbeddingCacheStore? _cacheStore;
    private readonly ILogger<SemanticSearchService> _logger;
    private readonly int _embeddingBatchDelaySeconds;
    private readonly double _semanticThreshold;
    private readonly double _semanticWeight;

    public SemanticSearchService(
        IAppDbContext db,
        IEmbeddingProvider embeddings,
        ILogger<SemanticSearchService> logger,
        IEmbeddingCacheStore? cacheStore = null,
        IOptions<GeminiOptions>? geminiOptions = null)
    {
        _db = db;
        _embeddings = embeddings;
        _logger = logger;
        _cacheStore = cacheStore;
        _embeddingBatchDelaySeconds = Math.Clamp(geminiOptions?.Value.EmbeddingBatchDelaySeconds ?? 3, 0, 60);
        _semanticThreshold = Math.Clamp(geminiOptions?.Value.RetrievalSemanticThreshold ?? 0.38, 0, 1);
        _semanticWeight = Math.Clamp(geminiOptions?.Value.RetrievalSemanticWeight ?? 0.5, 0, 1);
    }

    public async Task<bool> HasLexicalEvidenceAsync(string query, AssistantChatFilters? filters, CancellationToken ct = default)
    {
        var dbQuery = _db.Products.AsNoTracking().Include(p => p.Category).Include(p => p.Brand).Include(p => p.Seller).Include(p => p.Reviews).AsQueryable();
        if (filters?.CategoryId is int categoryId) dbQuery = dbQuery.Where(p => p.CategoryId == categoryId);
        if (filters?.SellerId is int sellerId) dbQuery = dbQuery.Where(p => p.SellerId == sellerId);
        if (filters?.MinPrice is decimal minPrice) dbQuery = dbQuery.Where(p => p.Price >= minPrice);
        if (filters?.MaxPrice is decimal maxPrice) dbQuery = dbQuery.Where(p => p.Price <= maxPrice);
        if (filters?.InStockOnly == true) dbQuery = dbQuery.Where(p => p.Stock > 0);
        if (RetrievalScoring.TryFindSkuCandidate(query, out var skuCandidate))
        {
            var hasSkuCue = RetrievalScoring.HasNearbySkuCue(query, skuCandidate);
            var existsInCatalog = hasSkuCue || await _db.Products.AsNoTracking().AnyAsync(p => p.Sku == skuCandidate, ct);
            if (existsInCatalog) dbQuery = dbQuery.Where(p => p.Sku == skuCandidate.ToUpperInvariant());
        }

        var products = await dbQuery.OrderBy(p => p.Id).Take(MaxCatalogCandidates).ToListAsync(ct);
        if (products.Count == 0) return false;
        var productIds = products.Select(p => p.Id).ToArray();
        var attributes = await _db.ProductAttributeValues.AsNoTracking()
            .Include(value => value.AttributeDefinition)
            .Where(value => productIds.Contains(value.ProductId) && value.AttributeDefinition.IsActive)
            .OrderBy(value => value.AttributeDefinition.DisplayName)
            .ToListAsync(ct);
        var attributesByProduct = attributes.GroupBy(value => value.ProductId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<ProductAttributeValue>)group.ToArray());
        return products.SelectMany(product => BuildChunks(product, attributesByProduct.GetValueOrDefault(product.Id, [])))
            .Any(chunk => RetrievalScoring.LexicalScore(query, chunk.Content) > 0);
    }

    public async Task<IReadOnlyList<AssistantRetrievalCandidate>> SearchCandidatesAsync(
        string query, AssistantChatFilters? filters, int limit, CancellationToken ct = default)
    {
        var dbQuery = _db.Products.AsNoTracking().Include(p => p.Category).Include(p => p.Brand).Include(p => p.Seller).Include(p => p.Reviews).AsQueryable();
        if (filters?.CategoryId is int categoryId) dbQuery = dbQuery.Where(p => p.CategoryId == categoryId);
        if (filters?.SellerId is int sellerId) dbQuery = dbQuery.Where(p => p.SellerId == sellerId);
        if (filters?.MinPrice is decimal minPrice) dbQuery = dbQuery.Where(p => p.Price >= minPrice);
        if (filters?.MaxPrice is decimal maxPrice) dbQuery = dbQuery.Where(p => p.Price <= maxPrice);
        if (filters?.InStockOnly == true) dbQuery = dbQuery.Where(p => p.Stock > 0);
        if (RetrievalScoring.TryFindSkuCandidate(query, out var skuCandidate))
        {
            var hasSkuCue = RetrievalScoring.HasNearbySkuCue(query, skuCandidate);
            var existsInCatalog = hasSkuCue || await _db.Products.AsNoTracking().AnyAsync(p => p.Sku == skuCandidate, ct);
            if (existsInCatalog) dbQuery = dbQuery.Where(p => p.Sku == skuCandidate.ToUpperInvariant());
        }

        var products = await dbQuery.OrderBy(p => p.Id).Take(MaxCatalogCandidates).ToListAsync(ct);
        if (products.Count == 0) return [];
        var productIds = products.Select(p => p.Id).ToArray();
        var attributes = await _db.ProductAttributeValues.AsNoTracking()
            .Include(value => value.AttributeDefinition)
            .Where(value => productIds.Contains(value.ProductId) && value.AttributeDefinition.IsActive)
            .OrderBy(value => value.AttributeDefinition.DisplayName)
            .ToListAsync(ct);
        var attributesByProduct = attributes.GroupBy(value => value.ProductId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<ProductAttributeValue>)group.ToArray());
        var chunks = products.SelectMany(product => BuildChunks(product, attributesByProduct.GetValueOrDefault(product.Id, []))).ToArray();
        var explicitSkuQuery = RetrievalScoring.TryResolveSkuIntent(query, chunks.Select(chunk => new RetrievalScoringChunk
        {
            ProductId = chunk.ProductId,
            Type = chunk.ChunkType,
            Content = chunk.Content
        }), out var skuToken);

        float[] queryVector = [];
        try
        {
            await EmbeddingCachePopulationGate.WaitAsync(ct);
            try
            {
                if (!EmbeddingCacheLoadedFromFile)
                {
                    try
                    {
                        if (_cacheStore is not null)
                        {
                            foreach (var entry in await _cacheStore.ReadAsync(ct))
                                EmbeddingCache[entry.CacheKey] = entry.Vector;
                        }
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
                    {
                        _logger.LogWarning("Embedding file cache read skipped. Failure type: {FailureType}", ex.GetType().Name);
                    }
                    finally { EmbeddingCacheLoadedFromFile = true; }
                }

                var missing = chunks.GroupBy(CacheKey, StringComparer.Ordinal).Select(group => group.First())
                    .Where(chunk => !EmbeddingCache.ContainsKey(CacheKey(chunk))).ToArray();
                var batches = missing.Chunk(EmbeddingBatchSize).ToArray();
                for (var batchIndex = 0; batchIndex < batches.Length; batchIndex++)
                {
                    if (batchIndex > 0 && _embeddingBatchDelaySeconds > 0)
                    {
                        _logger.LogInformation("Embedding batch pacing: batch {Batch}/{BatchCount}, waiting {DelaySeconds} seconds.",
                            batchIndex + 1, batches.Length, _embeddingBatchDelaySeconds);
                        await Task.Delay(TimeSpan.FromSeconds(_embeddingBatchDelaySeconds), ct);
                    }

                    var batch = batches[batchIndex];
                    var vectors = await _embeddings.GenerateEmbeddingsAsync(batch.Select(chunk => chunk.Content).ToArray(), isQuery: false, ct);
                    if (vectors.Count != batch.Length) throw new AssistantProviderException("Embedding provider returned an invalid batch.");
                    for (var i = 0; i < batch.Length; i++) EmbeddingCache[CacheKey(batch[i])] = vectors[i];

                    if (_cacheStore is not null)
                    {
                        var entries = batch.Select(chunk =>
                        {
                            var parsed = ParseModelVersion(chunk.EmbeddingModelVersion);
                            return new EmbeddingCacheEntry(CacheKey(chunk), chunk.ContentHash, parsed.Model,
                                parsed.Dimensions, chunk.ProductId, chunk.ChunkType, EmbeddingCache[CacheKey(chunk)]);
                        }).ToArray();
                        await _cacheStore.WriteAsync(entries, ct);
                    }
                }

            }
            finally { EmbeddingCachePopulationGate.Release(); }

            var queryVectors = await _embeddings.GenerateEmbeddingsAsync([query], isQuery: true, ct);
            if (queryVectors.Count == 1)
            {
                queryVector = queryVectors[0];
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            _logger.LogWarning("Semantic retrieval unavailable; using lexical retrieval. Failure type: {FailureType}", ex.GetType().Name);
            queryVector = [];
        }

        var results = new List<AssistantRetrievalCandidate>();
        foreach (var product in products)
        {
            var productChunks = chunks.Where(chunk => chunk.ProductId == product.Id).ToArray();
            var scoringChunks = productChunks.Select(chunk => new RetrievalScoringChunk
            {
                ProductId = chunk.ProductId,
                Type = chunk.ChunkType,
                Content = chunk.Content,
                Vector = EmbeddingCache.GetValueOrDefault(CacheKey(chunk), [])
            }).ToArray();
            var score = RetrievalScoring.ScoreProduct(query, queryVector, scoringChunks, queryVector.Length > 0,
                _semanticThreshold, _semanticWeight, explicitSkuQuery, skuToken);
            if (!score.Included) continue;
            var sources = score.IncludedChunkIndexes.Select(index => productChunks[index]).ToArray();
            results.Add(new AssistantRetrievalCandidate(ToDto(product), score.LexicalScore, score.SemanticScore,
                score.CombinedScore, sources));
        }
        return results.OrderByDescending(item => item.CombinedScore).ThenBy(item => item.Product.Id).Take(Math.Clamp(limit, 1, 8)).ToArray();
    }

    private IEnumerable<AssistantSourceChunk> BuildChunks(Product product, IReadOnlyList<ProductAttributeValue> attributes)
    {
        var category = product.Category?.Name ?? string.Empty;
        var brand = product.Brand?.Name;
        var summary = $"{product.Name}. SKU: {product.Sku}. Brand: {brand ?? "Unknown"}. Category: {category}.";
        yield return MakeChunk(product.Id, category, brand, "summary", summary);
        if (!string.IsNullOrWhiteSpace(product.Description))
            yield return MakeChunk(product.Id, category, brand, "description", product.Description.Trim()[..Math.Min(product.Description.Trim().Length, 1800)]);
        if (attributes.Count > 0)
        {
            var lines = attributes.Select(value => $"{value.AttributeDefinition.DisplayName}: {FormatValue(value)}" +
                (string.IsNullOrWhiteSpace(value.AttributeDefinition.Unit) ? string.Empty : $" {value.AttributeDefinition.Unit}"));
            yield return MakeChunk(product.Id, category, brand, "specifications", string.Join("\n", lines));
        }
    }

    private AssistantSourceChunk MakeChunk(int productId, string category, string? brand, string type, string content) =>
        new(productId, category, brand, type, content, GeminiAssistantProvider.Hash(content), _embeddings.ModelVersion);

    private static string FormatValue(ProductAttributeValue value) => value.DataType switch
    {
        CatalogAttributeType.Text => value.ValueText ?? string.Empty,
        CatalogAttributeType.Number => value.ValueNumber?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
        CatalogAttributeType.Boolean => value.ValueBoolean?.ToString() ?? string.Empty,
        _ => string.Empty
    };

    private static string CacheKey(AssistantSourceChunk chunk)
    {
        var parsed = ParseModelVersion(chunk.EmbeddingModelVersion);
        return parsed.Dimensions > 0
            ? LocalEmbeddingCacheStore.CreateKey(chunk.ContentHash, parsed.Model, parsed.Dimensions)
            : $"{chunk.ContentHash}:{chunk.EmbeddingModelVersion}";
    }

    private static (string Model, int Dimensions) ParseModelVersion(string modelVersion)
    {
        var separator = modelVersion.LastIndexOf(':');
        return separator > 0 && int.TryParse(modelVersion[(separator + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out var dimensions)
            ? (modelVersion[..separator], dimensions)
            : (modelVersion, 0);
    }

    private static ProductDto ToDto(Product p)
    {
        var count = p.Reviews?.Count ?? 0;
        var average = count == 0 ? 0 : Math.Round(p.Reviews!.Average(review => review.Rating), 2);
        return new ProductDto(p.Id, p.Name, p.Description, p.Price, p.Stock, p.ImageUrl, p.CategoryId,
            p.Category?.Name ?? string.Empty, p.SellerId, p.Seller?.ShopName ?? p.Seller?.FullName ?? string.Empty, average, count);
    }

}
