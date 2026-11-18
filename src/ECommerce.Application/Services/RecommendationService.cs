using ECommerce.Application.DTOs.Recommendations;
using ECommerce.Application.Interfaces;
using ECommerce.Application.Options;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ECommerce.Application.Services;

public sealed class RecommendationService : IRecommendationService
{
    private static readonly OrderStatus[] EligibleOrderStatuses =
    [OrderStatus.Paid, OrderStatus.Shipped, OrderStatus.Delivered];

    private readonly IAppDbContext _db;
    private readonly RecommendationOptions _options;

    public RecommendationService(IAppDbContext db, IOptions<RecommendationOptions> options)
    {
        _db = db;
        _options = options.Value;
    }

    public async Task<RecommendationResponseDto> GetForUserAsync(
        int? userId,
        int? take = null,
        DateTime? asOfUtc = null,
        DateTime? cutoffExclusiveUtc = null,
        CancellationToken cancellationToken = default, IReadOnlyCollection<int>? eligibleProductIds = null)
    {
        var asOf = NormalizeUtc(asOfUtc ?? DateTime.UtcNow);
        var cutoff = NormalizeUtc(cutoffExclusiveUtc ?? asOf);
        var resultCount = Math.Clamp(take ?? _options.DefaultResultCount, 1, 100);
        var categoryIds = await LoadCategoryIdsAsync(cutoff, cancellationToken);
        var catalog = await LoadCatalogAsync(cutoff, categoryIds, cancellationToken);
        if (catalog.Count == 0)
            return Response("empty_catalog", asOf, []);
        var features = await LoadFeaturesAsync(catalog, cancellationToken);
        if (eligibleProductIds is not null) catalog = catalog.Where(product => eligibleProductIds.Contains(product.Id)).ToList();

        List<Order> eligibleOrders = userId is null
            ? []
            : await LoadEligibleOrdersAsync(userId.Value, cutoff, categoryIds, cancellationToken);

        var profile = BuildPurchaseProfile(eligibleOrders, features);
        HashSet<int>? excludedPurchasedIds = !_options.AllowRePurchase && profile.Frequency > 0
            ? profile.ProductQuantities.Keys.ToHashSet()
            : null;
        if (profile.Frequency > 0)
        {
            var ranked = RankPersonalized(catalog, profile, features)
                .Where(candidate => candidate.Score > 0 && candidate.Score >= _options.PersonalizedMinimumScore)
                .ToList();
            var selected = ApplyDiversity(ranked, resultCount);
            if (selected.Count > 0)
                return Response("personalized", asOf, selected);
        }

        // A guest and a customer without eligible purchase history use the same
        // honest signal-based fallback: eligible order popularity, then ratings.
        var popular = await LoadPopularCountsAsync(cutoff, categoryIds, cancellationToken);
        var popularItems = ApplyDiversity(
            RankPopular(catalog, popular).Where(candidate => excludedPurchasedIds is null
                                                              || !excludedPurchasedIds.Contains(candidate.Product.Id)),
            resultCount);
        if (popularItems.Count > 0)
            return Response("popular", asOf, popularItems);

        var topRated = RankTopRated(catalog).Where(candidate => excludedPurchasedIds is null
                                                                 || !excludedPurchasedIds.Contains(candidate.Product.Id));
        var topRatedItems = ApplyDiversity(topRated, resultCount);
        if (topRatedItems.Count > 0) return Response("top_rated", asOf, topRatedItems);
        var fallback = catalog.Where(product => product.Stock > 0 &&
                (excludedPurchasedIds is null || !excludedPurchasedIds.Contains(product.Id)))
            .OrderByDescending(product => product.CreatedAt).ThenBy(product => product.Id).Take(resultCount)
            .Select(product => new RecommendedProductDto(product.ToDto(), 0,
                [new RecommendationReasonDto("catalog_available", "Sản phẩm đang có trong catalog")])).ToArray();
        return Response(fallback.Length > 0 ? "catalog_fallback" : "no_signal", asOf, fallback);
    }

    public async Task<RecommendationResponseDto> GetSimilarAsync(
        int targetProductId,
        int? take = null,
        DateTime? asOfUtc = null,
        DateTime? cutoffExclusiveUtc = null,
        CancellationToken cancellationToken = default, IReadOnlyCollection<int>? eligibleProductIds = null)
    {
        var asOf = NormalizeUtc(asOfUtc ?? DateTime.UtcNow);
        var cutoff = NormalizeUtc(cutoffExclusiveUtc ?? asOf);
        var resultCount = Math.Clamp(take ?? _options.DefaultResultCount, 1, 100);
        var categoryIds = await LoadCategoryIdsAsync(cutoff, cancellationToken);
        var catalog = await LoadCatalogAsync(cutoff, categoryIds, cancellationToken);
        var target = catalog.FirstOrDefault(product => product.Id == targetProductId);
        if (target is null)
            return Response("similar_no_match", asOf, []);
        var features = await LoadFeaturesAsync(catalog, cancellationToken);
        if (eligibleProductIds is not null) catalog = catalog.Where(product => eligibleProductIds.Contains(product.Id)).ToList();

        var ranked = catalog
            .Where(candidate => candidate.Id != target.Id && candidate.Stock > 0)
            .Select(candidate => ScoreSimilar(target, candidate, features))
            .Where(candidate => candidate.Score > 0 && candidate.Score >= _options.SimilarMinimumScore)
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Product.Id)
            .ToList();

        var selected = ApplyDiversity(ranked, resultCount);
        return Response(selected.Count > 0 ? "similar" : "similar_no_match", asOf, selected);
    }

    internal static PurchaseProfile BuildPurchaseProfile(IReadOnlyCollection<Order> orders,
        IReadOnlyDictionary<int, ProductFeatures>? featuresByProduct = null)
    {
        var distinctOrders = orders
            .Where(order => order.Items.Any(item => item.Status != FulfillmentStatus.Cancelled))
            .GroupBy(order => order.Id)
            .Select(group => group.First())
            .ToList();

        var eligibleItems = distinctOrders
            .SelectMany(order => order.Items
                .Where(item => item.Status != FulfillmentStatus.Cancelled)
                .Select(item => new PurchaseSignal(item, order.Id)))
            .ToList();

        // Each order contributes to a category at most once. This makes category
        // frequency a distinct-order signal rather than an item-line count.
        var categoryOrderCounts = eligibleItems
            .GroupBy(signal => signal.Item.Product.CategoryId)
            .ToDictionary(group => group.Key, group => group.Select(x => x.OrderId).Distinct().Count());
        var brandOrderCounts = eligibleItems
            .Where(signal => featuresByProduct?.GetValueOrDefault(signal.Item.ProductId)?.BrandId is not null)
            .GroupBy(signal => featuresByProduct![signal.Item.ProductId].BrandId!.Value)
            .ToDictionary(group => group.Key, group => group.Select(signal => signal.OrderId).Distinct().Count());
        var purchasedFeatures = eligibleItems
            .Where(signal => featuresByProduct?.ContainsKey(signal.Item.ProductId) == true)
            .SelectMany(signal => featuresByProduct![signal.Item.ProductId].Attributes
                .Select(feature => new PurchasedAttributeSignal(feature, signal.Item.Quantity)))
            .ToArray();
        var characteristicSignals = eligibleItems
            .Where(signal => featuresByProduct?.ContainsKey(signal.Item.ProductId) == true)
            .Select(signal => new PurchasedCharacteristicSignal(
                featuresByProduct![signal.Item.ProductId].CategoryId,
                featuresByProduct[signal.Item.ProductId].Name,
                featuresByProduct[signal.Item.ProductId].Description))
            .ToArray();

        return new PurchaseProfile(
            distinctOrders.Count,
            distinctOrders.Sum(order => order.Payment?.Amount ?? 0m),
            categoryOrderCounts,
            eligibleItems.GroupBy(signal => signal.Item.ProductId)
                .ToDictionary(group => group.Key, group => group.Sum(x => x.Item.Quantity)),
            eligibleItems.Select(signal => signal.Item.UnitPrice).Where(price => price > 0).ToArray(),
            brandOrderCounts, purchasedFeatures, characteristicSignals);
    }

    private async Task<List<int>> LoadCategoryIdsAsync(DateTime cutoff, CancellationToken ct) =>
        await _db.Categories
            .AsNoTracking()
            .Where(category => category.CreatedAt < cutoff)
            .Select(category => category.Id)
            .ToListAsync(ct);

    private async Task<List<Product>> LoadCatalogAsync(DateTime cutoff, IReadOnlyCollection<int> categoryIds, CancellationToken ct) =>
        await _db.Products
            .AsNoTracking()
            .Where(product => product.CreatedAt < cutoff && categoryIds.Contains(product.CategoryId))
            .Where(product => product.Seller.Role != UserRole.Seller || product.Seller.SellerStatus == SellerStatus.Approved)
            .Include(product => product.Category)
            .Include(product => product.Seller)
            .Include(product => product.Brand)
            .Include(product => product.AttributeValues).ThenInclude(value => value.AttributeDefinition)
            .Include(product => product.Reviews.Where(review => review.CreatedAt < cutoff))
            .ToListAsync(ct);

    private async Task<IReadOnlyDictionary<int, ProductFeatures>> LoadFeaturesAsync(
        IReadOnlyCollection<Product> products, CancellationToken ct)
    {
        var productIds = products.Select(product => product.Id).ToArray();
        var values = await _db.ProductAttributeValues.AsNoTracking()
            .Include(value => value.AttributeDefinition)
            .Where(value => productIds.Contains(value.ProductId) && value.AttributeDefinition.IsActive && value.AttributeDefinition.IsComparable)
            .ToListAsync(ct);
        var attributesByProduct = values.GroupBy(value => value.ProductId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<ProductFeature>)group.Select(value => new ProductFeature(
                value.CategoryId,
                value.AttributeDefinition.Code,
                value.AttributeDefinition.Unit,
                value.DataType,
                value.ValueText,
                value.ValueNumber,
                value.ValueBoolean)).ToArray());

        return products.ToDictionary(product => product.Id, product => new ProductFeatures(
            product.CategoryId,
            product.BrandId,
            product.Name,
            product.Description,
            attributesByProduct.GetValueOrDefault(product.Id, [])));
    }

    private async Task<List<Order>> LoadEligibleOrdersAsync(
        int userId,
        DateTime cutoff,
        IReadOnlyCollection<int> categoryIds,
        CancellationToken ct) =>
        await _db.Orders.AsNoTracking().EligiblePurchases(cutoff)
            .Where(order => order.UserId == userId)
            .Include(order => order.Payment)
            .Include(order => order.Items.Where(item => item.CreatedAt < cutoff
                                                        && item.Status != FulfillmentStatus.Cancelled
                                                        && item.Product.CreatedAt < cutoff
                                                        && categoryIds.Contains(item.Product.CategoryId)))
                .ThenInclude(item => item.Product)
            .ToListAsync(ct);

    private async Task<Dictionary<int, int>> LoadPopularCountsAsync(
        DateTime cutoff,
        IReadOnlyCollection<int> categoryIds,
        CancellationToken ct)
    {
        var orders = await _db.Orders.AsNoTracking().EligiblePurchases(cutoff)
            .Include(order => order.Items.Where(item => item.CreatedAt < cutoff
                                                        && item.Status != FulfillmentStatus.Cancelled
                                                        && item.Product.CreatedAt < cutoff
                                                        && categoryIds.Contains(item.Product.CategoryId)))
            .ToListAsync(ct);

        return orders.SelectMany(order => order.Items)
            .GroupBy(item => item.ProductId)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.Quantity));
    }

    private List<ScoredProduct> RankPersonalized(IReadOnlyList<Product> catalog, PurchaseProfile profile,
        IReadOnlyDictionary<int, ProductFeatures> featuresByProduct)
    {
        var denominator = Math.Max(1, profile.Frequency);
        var meanHistoricalPrice = profile.HistoricalPrices.Length == 0
            ? (decimal?)null
            : profile.HistoricalPrices.Average();

        return catalog
            .Where(product => product.Stock > 0)
            .Where(product => _options.AllowRePurchase || !profile.ProductQuantities.ContainsKey(product.Id))
            .Select(product =>
            {
                var reasons = new List<RecommendationReasonDto>();
                var score = 0d;

                if (profile.CategoryOrderCounts.TryGetValue(product.CategoryId, out var categoryOrders))
                {
                    var contribution = _options.CategoryWeight * categoryOrders / denominator;
                    if (contribution > 0)
                    {
                        score += contribution;
                        reasons.Add(new("same_category", "Cùng danh mục bạn đã mua"));
                    }
                }

                if (featuresByProduct.TryGetValue(product.Id, out var candidateFeatures) &&
                    candidateFeatures.BrandId is int candidateBrandId &&
                    profile.BrandOrderCounts.TryGetValue(candidateBrandId, out var brandOrders))
                {
                    var contribution = _options.BrandWeight * brandOrders / denominator;
                    if (contribution > 0)
                    {
                        score += contribution;
                        reasons.Add(new("same_brand", "Cùng thương hiệu bạn đã mua"));
                    }
                }

                if (featuresByProduct.TryGetValue(product.Id, out candidateFeatures))
                {
                    var attributeAffinity = ScoreAttributeAffinity(profile.PurchasedAttributeSignals,
                        candidateFeatures.Attributes);
                    var attributeContribution = _options.AttributeWeight * attributeAffinity;
                    if (attributeContribution > 0)
                    {
                        score += attributeContribution;
                        reasons.Add(new("matching_attributes", "Có thuộc tính gần với sản phẩm bạn đã mua"));
                    }

                    var characteristicAffinity = ScoreCharacteristicAffinity(profile.CharacteristicSignals,
                        candidateFeatures);
                    var characteristicContribution = _options.CharacteristicWeight * characteristicAffinity;
                    if (characteristicContribution > 0)
                    {
                        score += characteristicContribution;
                        reasons.Add(new("similar_characteristics", "Đặc điểm sản phẩm gần với lịch sử mua hàng"));
                    }
                }

                if (meanHistoricalPrice is > 0)
                {
                    var relativeDifference = Math.Abs(product.Price - meanHistoricalPrice.Value) / meanHistoricalPrice.Value;
                    if (relativeDifference <= _options.MaximumRelativePriceDifference)
                    {
                        var contribution = _options.PriceWeight
                            * (double)Math.Max(0m, 1m - relativeDifference);
                        if (contribution > 0)
                        {
                            score += contribution;
                            reasons.Add(new("similar_price", "Giá gần với sản phẩm bạn đã mua"));
                        }
                    }
                }

                if (profile.ProductQuantities.ContainsKey(product.Id) && _options.AllowRePurchase
                    && _options.RepeatPurchaseWeight > 0)
                {
                    score += _options.RepeatPurchaseWeight;
                    reasons.Add(new("repeat_purchase", "Có thể mua lại sản phẩm này"));
                }

                return new ScoredProduct(product, score, reasons);
            })
            .Where(candidate => candidate.Score > 0)
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Product.Id)
            .ToList();
    }

    private ScoredProduct ScoreSimilar(Product target, Product candidate,
        IReadOnlyDictionary<int, ProductFeatures> featuresByProduct)
    {
        var reasons = new List<RecommendationReasonDto>();
        var score = 0d;
        if (candidate.CategoryId == target.CategoryId && _options.SimilarCategoryWeight > 0)
        {
            score += _options.SimilarCategoryWeight;
            reasons.Add(new("same_category", "Cùng danh mục"));
        }

        if (featuresByProduct.TryGetValue(target.Id, out var targetFeatures) &&
            featuresByProduct.TryGetValue(candidate.Id, out var candidateFeatures))
        {
            if (targetFeatures.BrandId is not null && targetFeatures.BrandId == candidateFeatures.BrandId &&
                _options.SimilarBrandWeight > 0)
            {
                score += _options.SimilarBrandWeight;
                reasons.Add(new("same_brand", "Cùng thương hiệu"));
            }

            var attributeSimilarity = ScoreAttributeSimilarity(targetFeatures.Attributes, candidateFeatures.Attributes);
            var attributeContribution = _options.SimilarAttributeWeight * attributeSimilarity;
            if (attributeContribution > 0)
            {
                score += attributeContribution;
                reasons.Add(new("similar_attributes", "Có thuộc tính có cấu trúc tương đồng"));
            }

            var characteristicSimilarity = TextSimilarity(
                $"{targetFeatures.Name} {targetFeatures.Description}",
                $"{candidateFeatures.Name} {candidateFeatures.Description}");
            var characteristicContribution = _options.SimilarCharacteristicWeight * characteristicSimilarity;
            if (characteristicContribution > 0)
            {
                score += characteristicContribution;
                reasons.Add(new("similar_characteristics", "Tên hoặc mô tả sản phẩm có đặc điểm tương đồng"));
            }
        }

        if (target.Price > 0)
        {
            var relativeDifference = Math.Abs(candidate.Price - target.Price) / target.Price;
            if (relativeDifference <= _options.MaximumRelativePriceDifference)
            {
                var contribution = _options.SimilarPriceWeight
                    * (double)Math.Max(0m, 1m - relativeDifference);
                if (contribution > 0)
                {
                    score += contribution;
                    reasons.Add(new("similar_price", "Giá tương đối gần"));
                }
            }
        }

        return new ScoredProduct(candidate, score, reasons);
    }

    private double ScoreAttributeAffinity(IReadOnlyList<PurchasedAttributeSignal> preferences,
        IReadOnlyList<ProductFeature> candidateAttributes)
    {
        if (preferences.Count == 0 || candidateAttributes.Count == 0) return 0;
        double weightedScore = 0;
        double totalWeight = 0;
        foreach (var preference in preferences)
        {
            var best = candidateAttributes
                .Where(candidate => SameAttributeDefinition(preference.Feature, candidate))
                .Select(candidate => AttributeValueSimilarity(preference.Feature, candidate))
                .DefaultIfEmpty(0)
                .Max();
            var weight = Math.Max(1, preference.Quantity);
            weightedScore += best * weight;
            totalWeight += weight;
        }
        return totalWeight <= 0 ? 0 : weightedScore / totalWeight;
    }

    private double ScoreAttributeSimilarity(IReadOnlyList<ProductFeature> left, IReadOnlyList<ProductFeature> right)
    {
        if (left.Count == 0 || right.Count == 0) return 0;
        var matches = left.Select(feature => right
            .Where(candidate => SameAttributeDefinition(feature, candidate))
            .Select(candidate => AttributeValueSimilarity(feature, candidate))
            .DefaultIfEmpty(0).Max()).ToArray();
        return matches.Length == 0 ? 0 : matches.Average();
    }

    private double AttributeValueSimilarity(ProductFeature left, ProductFeature right)
    {
        if (left.DataType != right.DataType) return 0;
        if (left.DataType == CatalogAttributeType.Text)
            return NormalizeFeatureText(left.ValueText) == NormalizeFeatureText(right.ValueText) ? 1 : 0;
        if (left.DataType == CatalogAttributeType.Boolean)
            return left.ValueBoolean == right.ValueBoolean ? 1 : 0;
        if (left.ValueNumber is not decimal leftNumber || right.ValueNumber is not decimal rightNumber) return 0;
        var relativeDifference = Math.Abs(leftNumber - rightNumber) / Math.Max(1m, Math.Max(Math.Abs(leftNumber), Math.Abs(rightNumber)));
        var maximumDifference = Math.Max(0.01m, _options.MaximumAttributeRelativeDifference);
        return relativeDifference > maximumDifference
            ? 0
            : (double)Math.Max(0m, 1m - relativeDifference / maximumDifference);
    }

    private static bool SameAttributeDefinition(ProductFeature left, ProductFeature right) =>
        left.CategoryId == right.CategoryId && left.DataType == right.DataType &&
        StringComparer.OrdinalIgnoreCase.Equals(left.Code, right.Code) &&
        StringComparer.OrdinalIgnoreCase.Equals(left.Unit ?? string.Empty, right.Unit ?? string.Empty);

    private static double ScoreCharacteristicAffinity(IReadOnlyList<PurchasedCharacteristicSignal> preferences,
        ProductFeatures candidate)
    {
        if (preferences.Count == 0) return 0;
        return preferences.Where(preference => preference.CategoryId == candidate.CategoryId)
            .Select(preference => TextSimilarity($"{preference.Name} {preference.Description}",
                $"{candidate.Name} {candidate.Description}"))
            .DefaultIfEmpty(0).Max();
    }

    private static double TextSimilarity(string? left, string? right)
    {
        var leftTokens = FeatureTokens(left);
        var rightTokens = FeatureTokens(right);
        if (leftTokens.Count == 0 || rightTokens.Count == 0) return 0;
        var intersection = leftTokens.Count(token => rightTokens.Contains(token));
        var union = leftTokens.Union(rightTokens, StringComparer.Ordinal).Count();
        return union == 0 ? 0 : (double)intersection / union;
    }

    private static HashSet<string> FeatureTokens(string? value) =>
        NormalizeFeatureText(value).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(token => token.Length >= 3).ToHashSet(StringComparer.Ordinal);

    private static string NormalizeFeatureText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var decomposed = value.ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormD);
        var builder = new System.Text.StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(character) == System.Globalization.UnicodeCategory.NonSpacingMark)
                continue;
            builder.Append(character == 'đ' ? 'd' : character);
        }
        return System.Text.RegularExpressions.Regex.Replace(builder.ToString(), @"[^\p{L}\p{N}]+", " ").Trim();
    }

    private List<ScoredProduct> RankPopular(
        IReadOnlyList<Product> catalog,
        IReadOnlyDictionary<int, int> popularity) =>
        catalog
            .Where(product => product.Stock > 0 && popularity.TryGetValue(product.Id, out var count) && count > 0)
            .Select(product => new ScoredProduct(
                product,
                popularity[product.Id],
                [new RecommendationReasonDto("popular", "Được mua trong các đơn đủ điều kiện")]))
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Product.Id)
            .ToList();

    private List<ScoredProduct> RankTopRated(IReadOnlyList<Product> catalog) =>
        catalog
            .Where(product => product.Stock > 0 && product.Reviews.Count > 0)
            .Select(product => new ScoredProduct(
                product,
                product.Reviews.Average(review => review.Rating) / 5d,
                [new RecommendationReasonDto("top_rated", "Được khách hàng đánh giá cao")]))
            .Where(candidate => candidate.Score > 0)
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Product.Id)
            .ToList();

    private List<RecommendedProductDto> ApplyDiversity(IEnumerable<ScoredProduct> ranked, int take)
    {
        var maxPerCategory = Math.Max(1, _options.MaximumProductsPerCategory);
        var categoryCounts = new Dictionary<int, int>();
        var seenProductIds = new HashSet<int>();
        var selected = new List<RecommendedProductDto>();

        foreach (var candidate in ranked.OrderByDescending(item => item.Score).ThenBy(item => item.Product.Id))
        {
            var product = candidate.Product;
            if (candidate.Score <= 0 || !seenProductIds.Add(product.Id))
                continue;
            var categoryCount = categoryCounts.GetValueOrDefault(product.CategoryId);
            if (categoryCount >= maxPerCategory)
                continue;

            categoryCounts[product.CategoryId] = categoryCount + 1;
            selected.Add(new RecommendedProductDto(product.ToDto(), candidate.Score, candidate.Reasons));
            if (selected.Count == take)
                break;
        }

        return selected;
    }

    private static RecommendationResponseDto Response(
        string strategy,
        DateTime generatedAtUtc,
        IReadOnlyList<RecommendedProductDto> items) =>
        new(strategy, generatedAtUtc, items);

    private static DateTime NormalizeUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    internal sealed record PurchaseProfile(
        int Frequency,
        decimal Monetary,
        IReadOnlyDictionary<int, int> CategoryOrderCounts,
        IReadOnlyDictionary<int, int> ProductQuantities,
        decimal[] HistoricalPrices,
        IReadOnlyDictionary<int, int> BrandOrderCounts,
        IReadOnlyList<PurchasedAttributeSignal> PurchasedAttributeSignals,
        IReadOnlyList<PurchasedCharacteristicSignal> CharacteristicSignals);

    internal sealed record ProductFeatures(
        int CategoryId,
        int? BrandId,
        string Name,
        string? Description,
        IReadOnlyList<ProductFeature> Attributes);
    internal sealed record ProductFeature(int CategoryId, string Code, string? Unit, CatalogAttributeType DataType,
        string? ValueText, decimal? ValueNumber, bool? ValueBoolean);
    internal sealed record PurchasedAttributeSignal(ProductFeature Feature, int Quantity);
    internal sealed record PurchasedCharacteristicSignal(int CategoryId, string Name, string? Description);
    private sealed record PurchaseSignal(OrderItem Item, int OrderId);
    private sealed record ScoredProduct(Product Product, double Score, IReadOnlyList<RecommendationReasonDto> Reasons);
}
