using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ECommerce.Application.DTOs.AI;
using ECommerce.Application.DTOs.Catalog;
using ECommerce.Application.Interfaces;
using ECommerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ECommerce.Infrastructure.AI;

public sealed class SemanticSearchService : ISemanticSearchService
{
    private const int MaxCatalogCandidates = 1000;
    private const int EmbeddingBatchSize = 16;
    private const int MaxConcurrentEmbeddingBatches = 1;
    private static readonly Regex ExplicitSkuPattern = new(@"\b[A-Z0-9]{2,}(?:-[A-Z0-9]{2,}){2,}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly ConcurrentDictionary<string, float[]> EmbeddingCache = new(StringComparer.Ordinal);
    private static readonly SemaphoreSlim EmbeddingCachePopulationGate = new(1, 1);
    private static bool EmbeddingCacheLoadedFromFile;
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "toi", "minh", "ban", "cho", "voi", "la", "co", "khong", "nao", "gi", "nhe", "nha", "va", "cua", "mot",
        "cac", "nhung", "hay", "duoc", "can", "muon", "tim", "mua", "loai", "san", "pham", "hang", "shop", "gia",
        "re", "de", "duoi", "tren", "tam", "khoang", "the", "please", "find", "show", "me", "some", "for", "with", "under", "over"
    };

    private readonly IAppDbContext _db;
    private readonly IEmbeddingProvider _embeddings;
    private readonly IEmbeddingCacheStore? _cacheStore;
    private readonly ILogger<SemanticSearchService> _logger;

    public SemanticSearchService(
        IAppDbContext db,
        IEmbeddingProvider embeddings,
        ILogger<SemanticSearchService> logger,
        IEmbeddingCacheStore? cacheStore = null)
    {
        _db = db;
        _embeddings = embeddings;
        _logger = logger;
        _cacheStore = cacheStore;
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
        var explicitSku = ExplicitSkuPattern.Match(query);
        if (explicitSku.Success) dbQuery = dbQuery.Where(p => p.Sku == explicitSku.Value.ToUpperInvariant());

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
        var normalizedQuery = Normalize(query);
        var terms = normalizedQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(term => term.Length > 1 && !StopWords.Contains(term)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var chunks = products.SelectMany(product => BuildChunks(product, attributesByProduct.GetValueOrDefault(product.Id, []))).ToArray();

        var semanticByChunk = new Dictionary<string, double>(StringComparer.Ordinal);
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
                using var batchGate = new SemaphoreSlim(MaxConcurrentEmbeddingBatches, MaxConcurrentEmbeddingBatches);
                var batchTasks = missing.Chunk(EmbeddingBatchSize).Select(async batch =>
                {
                    await batchGate.WaitAsync(ct);
                    try
                    {
                        var vectors = await _embeddings.GenerateEmbeddingsAsync(batch.Select(chunk => chunk.Content).ToArray(), isQuery: false, ct);
                        if (vectors.Count != batch.Length) throw new AssistantProviderException("Embedding provider returned an invalid batch.");
                        for (var i = 0; i < batch.Length; i++) EmbeddingCache[CacheKey(batch[i])] = vectors[i];
                    }
                    finally { batchGate.Release(); }
                }).ToArray();
                await Task.WhenAll(batchTasks);

                if (_cacheStore is not null && missing.Length > 0)
                {
                    try
                    {
                        var entries = missing.Select(chunk =>
                        {
                            var parsed = ParseModelVersion(chunk.EmbeddingModelVersion);
                            return new EmbeddingCacheEntry(CacheKey(chunk), chunk.ContentHash, parsed.Model,
                                parsed.Dimensions, chunk.ProductId, chunk.ChunkType, EmbeddingCache[CacheKey(chunk)]);
                        }).ToArray();
                        await _cacheStore.WriteAsync(entries, ct);
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
                    {
                        _logger.LogWarning("Embedding file cache write skipped. Failure type: {FailureType}", ex.GetType().Name);
                    }
                }
            }
            finally { EmbeddingCachePopulationGate.Release(); }

            var queryVectors = await _embeddings.GenerateEmbeddingsAsync([query], isQuery: true, ct);
            if (queryVectors.Count == 1)
            {
                foreach (var chunk in chunks)
                    semanticByChunk[CacheKey(chunk)] = Cosine(queryVectors[0], EmbeddingCache[CacheKey(chunk)]);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            _logger.LogWarning("Semantic retrieval unavailable; using lexical retrieval. Failure type: {FailureType}", ex.GetType().Name);
            semanticByChunk.Clear();
        }

        var results = new List<AssistantRetrievalCandidate>();
        foreach (var product in products)
        {
            var productChunks = chunks.Where(chunk => chunk.ProductId == product.Id).ToArray();
            var lexical = productChunks.Select(chunk => LexicalScore(normalizedQuery, terms, Normalize(chunk.Content))).DefaultIfEmpty().Max();
            var semantic = productChunks.Select(chunk => semanticByChunk.GetValueOrDefault(CacheKey(chunk))).DefaultIfEmpty().Max();
            var combined = semanticByChunk.Count == 0 ? lexical : (0.5 * lexical) + (0.5 * Math.Max(0, semantic));
            if (lexical <= 0 && semantic < 0.38 && combined < 0.22) continue;
            results.Add(new AssistantRetrievalCandidate(ToDto(product), lexical, semantic, combined,
                productChunks.Where(chunk => lexical > 0 || semanticByChunk.GetValueOrDefault(CacheKey(chunk)) >= 0.38).ToArray()));
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

    private static double LexicalScore(string query, IReadOnlyList<string> terms, string content)
    {
        if (query.Length == 0 || content.Length == 0) return 0;
        var score = content.Contains(query, StringComparison.OrdinalIgnoreCase) ? 0.8 : 0;
        var contentWords = content.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (terms.Count > 0) score += terms.Count(term => contentWords.Contains(term)) / (double)terms.Count * 0.7;
        return Math.Min(1, score);
    }

    private static string Normalize(string value)
    {
        var decomposed = value.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark) builder.Append(ch == 'đ' ? 'd' : ch);
        return string.Join(' ', new string(builder.ToString().Select(ch => char.IsLetterOrDigit(ch) ? ch : ' ').ToArray())
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static double Cosine(IReadOnlyList<float> left, IReadOnlyList<float> right)
    {
        if (left.Count == 0 || left.Count != right.Count) return 0;
        double dot = 0, leftNorm = 0, rightNorm = 0;
        for (var i = 0; i < left.Count; i++) { dot += left[i] * right[i]; leftNorm += left[i] * left[i]; rightNorm += right[i] * right[i]; }
        return leftNorm == 0 || rightNorm == 0 ? 0 : dot / (Math.Sqrt(leftNorm) * Math.Sqrt(rightNorm));
    }
}
