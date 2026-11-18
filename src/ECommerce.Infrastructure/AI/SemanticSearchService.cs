using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using ECommerce.Application.DTOs.AI;
using ECommerce.Application.DTOs.Catalog;
using ECommerce.Application.Interfaces;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ECommerce.Infrastructure.AI;

public sealed class SemanticSearchService : ISemanticSearchService
{
    private const int MaxCatalogCandidates = 1000;
    private const int EmbeddingBatchSize = 16;
    private readonly EmbeddingRuntimeState _state;

    private readonly IAppDbContext _db;
    private readonly IEmbeddingProvider _embeddings;
    private readonly IEmbeddingCacheStore? _cacheStore;
    private readonly ILogger<SemanticSearchService> _logger;
    private readonly int _embeddingBatchDelaySeconds;
    private readonly double _semanticThreshold;
    private readonly double _semanticWeight;
    private readonly AssistantDomainOptions _domainOptions;
    private readonly bool _lexicalFallbackWhileWarming;

    public bool IsHybridReady => Volatile.Read(ref _state.WarmupCompleted) != 0 && Volatile.Read(ref _state.WarmupEmbeddingFailed) == 0;
    public string RetrievalMode => IsHybridReady ? "hybrid" :
        Volatile.Read(ref _state.WarmupScheduled) != 0 && Volatile.Read(ref _state.WarmupCompleted) == 0 ? "lexical-warming" : "lexical";

    public void MarkWarmupScheduled()
    {
        Volatile.Write(ref _state.WarmupEmbeddingFailed, 0);
        Volatile.Write(ref _state.WarmupCompleted, 0);
        Volatile.Write(ref _state.WarmupScheduled, 1);
    }

    public void MarkWarmupDisabled()
    {
        Volatile.Write(ref _state.WarmupScheduled, 0);
        Volatile.Write(ref _state.WarmupEmbeddingFailed, 0);
        Volatile.Write(ref _state.WarmupCompleted, 1);
    }

    public async Task WarmUpAsync(CancellationToken ct = default)
    {
        if (Interlocked.CompareExchange(ref _state.WarmupRunning, 1, 0) != 0) return;
        Volatile.Write(ref _state.WarmupCompleted, 0);
        _state.IsWarmupCall.Value = true;
        Volatile.Write(ref _state.WarmupEmbeddingFailed, 0);
        try
        {
            if (!await _db.Products.AsNoTracking().AnyAsync(ct))
            {
                Volatile.Write(ref _state.WarmupEmbeddingFailed, 1);
                return;
            }
            await SearchCandidatesAsync("catalog warmup", null, 8, ct);
            if (Volatile.Read(ref _state.WarmupEmbeddingFailed) == 0)
            {
                var queryVectors = await _embeddings.GenerateEmbeddingsAsync(["catalog warmup"], isQuery: true, ct);
                if (queryVectors.Count != 1 || queryVectors[0].Length == 0)
                    Volatile.Write(ref _state.WarmupEmbeddingFailed, 1);
            }
            if (Volatile.Read(ref _state.WarmupEmbeddingFailed) == 0) Volatile.Write(ref _state.WarmupCompleted, 1);
        }
        catch
        {
            Volatile.Write(ref _state.WarmupEmbeddingFailed, 1);
            throw;
        }
        finally
        {
            if (Volatile.Read(ref _state.WarmupEmbeddingFailed) != 0) Volatile.Write(ref _state.WarmupCompleted, 1);
            _state.IsWarmupCall.Value = false;
            Volatile.Write(ref _state.WarmupRunning, 0);
        }
    }

    public SemanticSearchService(
        IAppDbContext db,
        IEmbeddingProvider embeddings,
        ILogger<SemanticSearchService> logger,
        IEmbeddingCacheStore? cacheStore = null,
        IOptions<GeminiOptions>? geminiOptions = null,
        IOptions<AssistantDomainOptions>? domainOptions = null,
        EmbeddingRuntimeState? runtimeState = null)
    {
        _state = runtimeState ?? new EmbeddingRuntimeState();
        _db = db;
        _embeddings = embeddings;
        _logger = logger;
        _cacheStore = cacheStore;
        _embeddingBatchDelaySeconds = Math.Clamp(geminiOptions?.Value.EmbeddingBatchDelaySeconds ?? 3, 0, 60);
        _semanticThreshold = Math.Clamp(geminiOptions?.Value.RetrievalSemanticThreshold ?? 0.38, 0, 1);
        _semanticWeight = Math.Clamp(geminiOptions?.Value.RetrievalSemanticWeight ?? 0.5, 0, 1);
        _domainOptions = domainOptions?.Value ?? new AssistantDomainOptions();
        _lexicalFallbackWhileWarming = geminiOptions?.Value.LexicalFallbackWhileWarming ?? true;
    }

    public async Task<AssistantQueryAnalysis> AnalyzeQueryAsync(string query, CancellationToken ct = default)
    {
        var categories = await _db.Categories.AsNoTracking().ToListAsync(ct);
        var products = await _db.Products.AsNoTracking().ToListAsync(ct);
        var brands = await _db.Brands.AsNoTracking().ToListAsync(ct);
        var definitions = await _db.AttributeDefinitions.AsNoTracking().Where(definition => definition.IsActive).ToListAsync(ct);
        var values = await _db.ProductAttributeValues.AsNoTracking()
            .Include(value => value.AttributeDefinition)
            .Where(value => value.AttributeDefinition.IsActive).ToListAsync(ct);
        return AssistantQueryAnalyzer.Analyze(query, categories, products, brands, definitions, values, _domainOptions);
    }

    public async Task<bool> HasLexicalEvidenceAsync(string query, AssistantChatFilters? filters, CancellationToken ct = default)
    {
        var dbQuery = _db.Products.AsNoTracking().Where(p => p.Seller.Role != UserRole.Seller || p.Seller.SellerStatus == SellerStatus.Approved)
            .Include(p => p.Category).Include(p => p.Brand).Include(p => p.Seller).Include(p => p.Reviews).AsQueryable();
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
        => await SearchCandidatesAsync(query, filters, Array.Empty<AssistantTypedConstraint>(), limit, ct);

    public async Task<IReadOnlyList<AssistantRetrievalCandidate>> SearchCandidatesAsync(
        string query, AssistantChatFilters? filters, IReadOnlyList<AssistantTypedConstraint> constraints, int limit, CancellationToken ct = default)
    {
        var dbQuery = FilteredProducts(filters, constraints);
        if (dbQuery is null) return [];
        // Category aliases resolve to SQL category names before multilingual retrieval.
        foreach (var category in constraints.Where(c => c.CategoryId.HasValue && !string.IsNullOrWhiteSpace(c.ValueText)))
            query += " " + category.ValueText;
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
        foreach (var product in products) product.AttributeValues = attributesByProduct.GetValueOrDefault(product.Id, []).ToList();
        var chunks = products.SelectMany(product => BuildChunks(product, attributesByProduct.GetValueOrDefault(product.Id, []))).ToArray();
        var explicitSkuQuery = RetrievalScoring.TryResolveSkuIntent(query, chunks.Select(chunk => new RetrievalScoringChunk
        {
            ProductId = chunk.ProductId,
            Type = chunk.ChunkType,
            Content = chunk.Content
        }), out var skuToken);

        float[] queryVector = [];
        // shortcut: failed warm-up stays lexical until restart; add scheduled recovery for unattended deployments.
        if (_lexicalFallbackWhileWarming && Volatile.Read(ref _state.WarmupScheduled) != 0 &&
            (Volatile.Read(ref _state.WarmupCompleted) == 0 || Volatile.Read(ref _state.WarmupEmbeddingFailed) != 0) &&
            !_state.IsWarmupCall.Value)
            return BuildLexicalResults(query, products, chunks, limit, explicitSkuQuery, skuToken);
        try
        {
            await _state.EmbeddingCachePopulationGate.WaitAsync(ct);
            try
            {
                if (!_state.EmbeddingCacheLoadedFromFile)
                {
                    try
                    {
                        if (_cacheStore is not null)
                        {
                            foreach (var entry in await _cacheStore.ReadAsync(ct))
                                _state.EmbeddingCache[entry.CacheKey] = entry.Vector;
                        }
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
                    {
                        _logger.LogWarning("Embedding file cache read skipped. Failure type: {FailureType}", ex.GetType().Name);
                    }
                    finally { _state.EmbeddingCacheLoadedFromFile = true; }
                }

                var missing = chunks.GroupBy(CacheKey, StringComparer.Ordinal).Select(group => group.First())
                    .Where(chunk => !_state.EmbeddingCache.ContainsKey(CacheKey(chunk))).ToArray();
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
                    if (vectors.Count != batch.Length || vectors.Any(vector => vector is null || vector.Length == 0))
                        throw new AssistantProviderException("Embedding provider returned an invalid batch.");
                    for (var i = 0; i < batch.Length; i++) _state.EmbeddingCache[CacheKey(batch[i])] = vectors[i];

                    if (_cacheStore is not null)
                    {
                        var entries = batch.Select(chunk =>
                        {
                            var parsed = ParseModelVersion(chunk.EmbeddingModelVersion);
                            return new EmbeddingCacheEntry(CacheKey(chunk), chunk.ContentHash, parsed.Model,
                                parsed.Dimensions, chunk.ProductId, chunk.ChunkType, _state.EmbeddingCache[CacheKey(chunk)]);
                        }).ToArray();
                        await _cacheStore.WriteAsync(entries, ct);
                    }
                }

            }
            finally { _state.EmbeddingCachePopulationGate.Release(); }

            if (!_state.IsWarmupCall.Value)
            {
                var queryVectors = await _embeddings.GenerateEmbeddingsAsync([query], isQuery: true, ct);
                if (queryVectors.Count == 1) queryVector = queryVectors[0];
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            if (_state.IsWarmupCall.Value) Volatile.Write(ref _state.WarmupEmbeddingFailed, 1);
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
                Vector = _state.EmbeddingCache.GetValueOrDefault(CacheKey(chunk), [])
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

    private IReadOnlyList<AssistantRetrievalCandidate> BuildLexicalResults(string query, IReadOnlyList<Product> products,
        IReadOnlyList<AssistantSourceChunk> chunks, int limit, bool explicitSkuQuery, string skuToken)
    {
        var result = new List<AssistantRetrievalCandidate>();
        foreach (var product in products)
        {
            var productChunks = chunks.Where(chunk => chunk.ProductId == product.Id).ToArray();
            var scoringChunks = productChunks.Select(chunk => new RetrievalScoringChunk
            {
                ProductId = chunk.ProductId, Type = chunk.ChunkType, Content = chunk.Content
            }).ToArray();
            var scored = RetrievalScoring.ScoreProduct(query, [], scoringChunks, false, _semanticThreshold, 0,
                explicitSkuQuery, skuToken);
            if (!scored.Included) continue;
            var sources = scored.IncludedChunkIndexes.Select(index => productChunks[index]).ToArray();
            result.Add(new AssistantRetrievalCandidate(ToDto(product), scored.LexicalScore, 0,
                scored.CombinedScore, sources));
        }
        return result.OrderByDescending(item => item.CombinedScore).ThenBy(item => item.Product.Id)
            .Take(Math.Clamp(limit, 1, 8)).ToArray();
    }

    public async Task<IReadOnlyList<int>> FindEligibleProductIdsAsync(AssistantChatFilters? filters,
        IReadOnlyList<AssistantTypedConstraint> constraints, CancellationToken ct = default)
    {
        var query = FilteredProducts(filters, constraints);
        return query is null ? [] : await query.Where(p => p.Stock > 0).Select(p => p.Id).ToArrayAsync(ct);
    }

    private IQueryable<Product>? FilteredProducts(AssistantChatFilters? filters, IReadOnlyList<AssistantTypedConstraint> constraints)
    {
        var dbQuery = _db.Products.AsNoTracking().Where(p => p.Seller.Role != UserRole.Seller || p.Seller.SellerStatus == SellerStatus.Approved)
            .Include(p => p.Category).Include(p => p.Brand).Include(p => p.Seller).Include(p => p.Reviews).AsQueryable();
        if (filters?.CategoryId is int categoryId) dbQuery = dbQuery.Where(p => p.CategoryId == categoryId);
        if (filters?.SellerId is int sellerId) dbQuery = dbQuery.Where(p => p.SellerId == sellerId);
        if (filters?.MinPrice is decimal minPrice) dbQuery = dbQuery.Where(p => p.Price >= minPrice);
        if (filters?.MaxPrice is decimal maxPrice) dbQuery = dbQuery.Where(p => p.Price <= maxPrice);
        if (filters?.InStockOnly == true) dbQuery = dbQuery.Where(p => p.Stock > 0);
        foreach (var constraint in constraints)
        {
            if (constraint.CategoryId is int requestedCategoryId)
            {
                dbQuery = dbQuery.Where(product => product.CategoryId == requestedCategoryId);
                continue;
            }
            if (constraint.BrandId is int brandId)
            {
                if (brandId <= 0) return null;
                dbQuery = dbQuery.Where(product => product.BrandId == brandId);
                continue;
            }

            if (constraint.AttributeDefinitionId is not int definitionId) continue;
            var definitionIds = constraint.AttributeDefinitionIds ?? [definitionId];
            var valueQuery = _db.ProductAttributeValues.AsNoTracking()
                .Where(value => definitionIds.Contains(value.AttributeDefinitionId));
            if (constraint.DataType == (int)CatalogAttributeType.Number && constraint.ValueNumber is decimal number)
            {
                valueQuery = constraint.Operator switch
                {
                    "greaterThan" => valueQuery.Where(value => value.ValueNumber > number),
                    "greaterThanOrEqual" => valueQuery.Where(value => value.ValueNumber >= number),
                    "lessThan" => valueQuery.Where(value => value.ValueNumber < number),
                    "lessThanOrEqual" => valueQuery.Where(value => value.ValueNumber <= number),
                    _ => valueQuery.Where(value => value.ValueNumber == number)
                };
            }
            else if (constraint.DataType == (int)CatalogAttributeType.Boolean && constraint.ValueBoolean is bool boolean)
                valueQuery = valueQuery.Where(value => value.ValueBoolean == boolean);
            else if (constraint.DataType == (int)CatalogAttributeType.Text && !string.IsNullOrWhiteSpace(constraint.ValueText))
                valueQuery = valueQuery.Where(value => value.ValueText == constraint.ValueText);
            else
                return null;

            var matchingProductIds = valueQuery.Select(value => value.ProductId);
            dbQuery = dbQuery.Where(product => matchingProductIds.Contains(product.Id));
        }
        return dbQuery;
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
            p.Category?.Name ?? string.Empty, p.SellerId, p.Seller?.ShopName ?? p.Seller?.FullName ?? string.Empty, average, count,
            p.Sku, p.BrandId, p.Brand?.Name, p.AttributeValues.Select(value => new ProductAttributeDto(
                value.AttributeDefinitionId, value.AttributeDefinition.Code, value.AttributeDefinition.DisplayName, value.DataType,
                value.AttributeDefinition.Unit, value.ValueText, value.ValueNumber, value.ValueBoolean)).ToArray());
    }

}

public sealed class EmbeddingRuntimeState
{
    internal readonly ConcurrentDictionary<string, float[]> EmbeddingCache = new(StringComparer.Ordinal);
    internal readonly SemaphoreSlim EmbeddingCachePopulationGate = new(1, 1);
    internal bool EmbeddingCacheLoadedFromFile;
    internal int WarmupRunning;
    internal int WarmupScheduled;
    internal int WarmupCompleted;
    internal int WarmupEmbeddingFailed;
    internal readonly AsyncLocal<bool> IsWarmupCall = new();

}
