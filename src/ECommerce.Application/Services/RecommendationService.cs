using ECommerce.Application.DTOs.Recommendations;
using ECommerce.Application.Interfaces;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Application.Services;

public sealed class RecommendationService : IRecommendationService
{
    private const int DefaultLimit = 8;
    private const int MaximumLimit = 20;
    private static readonly OrderStatus[] EligibleOrderStatuses =
        [OrderStatus.Paid, OrderStatus.Shipped, OrderStatus.Delivered];

    private readonly IAppDbContext _db;

    public RecommendationService(IAppDbContext db) => _db = db;

    public async Task<RecommendationResponseDto> GetForUserAsync(
        int? userId,
        int? take = null,
        CancellationToken cancellationToken = default)
    {
        var limit = Math.Clamp(take ?? DefaultLimit, 1, MaximumLimit);
        var catalog = await PublicProducts()
            .Where(product => product.Stock > 0)
            .OrderBy(product => product.Id)
            .ToListAsync(cancellationToken);

        if (catalog.Count == 0)
            return Response("empty_catalog", []);

        HashSet<int> purchasedProductIds = [];
        if (userId is int customerId)
        {
            var orders = await LoadEligibleOrdersAsync(customerId, cancellationToken);
            var profile = BuildPurchaseProfile(orders);
            purchasedProductIds = profile.PurchasedProductIds;

            if (profile.OrderCount > 0)
            {
                var personalized = catalog
                    .Where(product => !profile.PurchasedProductIds.Contains(product.Id))
                    .Select(product => ScorePersonalized(product, profile))
                    .Where(item => item is not null)
                    .Select(item => item!)
                    .OrderByDescending(item => item.Score)
                    .ThenByDescending(item => item.Product.AverageRating)
                    .ThenByDescending(item => item.Product.ReviewCount)
                    .ThenBy(item => item.Product.Id)
                    .Take(limit)
                    .ToArray();

                if (personalized.Length > 0)
                    return Response("personalized", personalized);
            }
        }

        var popularCounts = await LoadPopularCountsAsync(cancellationToken);
        var popular = catalog
            .Where(product => !purchasedProductIds.Contains(product.Id))
            .Where(product => popularCounts.ContainsKey(product.Id))
            .OrderByDescending(product => popularCounts[product.Id])
            .ThenByDescending(product => product.Reviews.Count > 0
                ? product.Reviews.Average(review => review.Rating) : 0)
            .ThenBy(product => product.Id)
            .Take(limit)
            .Select(product => new RecommendedProductDto(
                product.ToDto(),
                popularCounts[product.Id],
                [new RecommendationReasonDto("popular_purchase", "Đã xuất hiện trong đơn hàng thanh toán thành công")]))
            .ToArray();

        if (popular.Length > 0)
            return Response("popular", popular);

        var rated = catalog
            .Where(product => !purchasedProductIds.Contains(product.Id) && product.Reviews.Count > 0)
            .OrderByDescending(product => product.Reviews.Average(review => review.Rating))
            .ThenByDescending(product => product.Reviews.Count)
            .ThenBy(product => product.Id)
            .Take(limit)
            .Select(product => new RecommendedProductDto(
                product.ToDto(),
                product.Reviews.Average(review => review.Rating) / 5d,
                [new RecommendationReasonDto("high_rating", "Được đánh giá tốt bởi khách hàng")]))
            .ToArray();

        if (rated.Length > 0)
            return Response("top_rated", rated);

        var fallback = catalog
            .Where(product => !purchasedProductIds.Contains(product.Id))
            .OrderByDescending(product => product.CreatedAt)
            .ThenBy(product => product.Id)
            .Take(limit)
            .Select(product => new RecommendedProductDto(
                product.ToDto(),
                0,
                [new RecommendationReasonDto("catalog_available", "Sản phẩm đang được bán trong cửa hàng")]))
            .ToArray();

        return Response("catalog_fallback", fallback);
    }

    public async Task<RecommendationResponseDto> GetSimilarAsync(
        int targetProductId,
        int? take = null,
        CancellationToken cancellationToken = default)
    {
        var limit = Math.Clamp(take ?? 4, 1, MaximumLimit);
        var target = await PublicProducts()
            .FirstOrDefaultAsync(product => product.Id == targetProductId, cancellationToken);

        if (target is null)
            return Response("target_unavailable", []);

        var targetAttributes = AttributeKeys(target);
        var candidates = await PublicProducts()
            .Where(product => product.Id != targetProductId && product.Stock > 0)
            .ToListAsync(cancellationToken);

        var similar = candidates
            .Select(product => ScoreSimilar(target, targetAttributes, product))
            .Where(item => item is not null && item.Score > 0)
            .Select(item => item!)
            .OrderByDescending(item => item.Score)
            .ThenByDescending(item => item.Product.AverageRating)
            .ThenByDescending(item => item.Product.ReviewCount)
            .ThenBy(item => item.Product.Id)
            .Take(limit)
            .ToArray();

        return Response(similar.Length > 0 ? "similar" : "no_similar_products", similar);
    }

    private IQueryable<Product> PublicProducts() =>
        _db.Products.AsNoTracking()
            .Where(product => product.Seller.Role != UserRole.Seller ||
                              product.Seller.SellerStatus == SellerStatus.Approved)
            .Include(product => product.Category)
            .Include(product => product.Seller)
            .Include(product => product.Brand)
            .Include(product => product.Reviews)
            .Include(product => product.AttributeValues)
                .ThenInclude(value => value.AttributeDefinition);

    private async Task<List<Order>> LoadEligibleOrdersAsync(int userId, CancellationToken ct) =>
        await _db.Orders.AsNoTracking()
            .Where(order => order.UserId == userId &&
                            EligibleOrderStatuses.Contains(order.Status) &&
                            order.Payment != null &&
                            order.Payment.Status == PaymentStatus.Completed)
            .Include(order => order.Payment)
            .Include(order => order.Items.Where(item => item.Status != FulfillmentStatus.Cancelled))
                .ThenInclude(item => item.Product)
                    .ThenInclude(product => product.Brand)
            .Include(order => order.Items.Where(item => item.Status != FulfillmentStatus.Cancelled))
                .ThenInclude(item => item.Product)
                    .ThenInclude(product => product.AttributeValues)
                        .ThenInclude(value => value.AttributeDefinition)
            .ToListAsync(ct);

    private async Task<Dictionary<int, int>> LoadPopularCountsAsync(CancellationToken ct)
    {
        var orders = await _db.Orders.AsNoTracking()
            .Where(order => EligibleOrderStatuses.Contains(order.Status) &&
                            order.Payment != null &&
                            order.Payment.Status == PaymentStatus.Completed)
            .Include(order => order.Items.Where(item => item.Status != FulfillmentStatus.Cancelled))
            .ToListAsync(ct);

        return orders.SelectMany(order => order.Items)
            .GroupBy(item => item.ProductId)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.Quantity));
    }

    private static PurchaseProfile BuildPurchaseProfile(IReadOnlyCollection<Order> orders)
    {
        var signals = orders
            .SelectMany(order => order.Items
                .Where(item => item.Status != FulfillmentStatus.Cancelled && item.Product is not null)
                .Select(item => new PurchaseSignal(order.Id, item.Product, item.UnitPrice, item.Quantity)))
            .ToArray();

        if (signals.Length == 0)
            return PurchaseProfile.Empty;

        var orderCount = signals.Select(signal => signal.OrderId).Distinct().Count();
        var categoryOrders = signals
            .GroupBy(signal => signal.Product.CategoryId)
            .ToDictionary(group => group.Key, group => group.Select(signal => signal.OrderId).Distinct().Count());
        var brandOrders = signals
            .Where(signal => signal.Product.BrandId.HasValue)
            .GroupBy(signal => signal.Product.BrandId!.Value)
            .ToDictionary(group => group.Key, group => group.Select(signal => signal.OrderId).Distinct().Count());
        var attributes = signals.SelectMany(signal => AttributeKeys(signal.Product)).ToHashSet(StringComparer.Ordinal);
        var prices = signals.Where(signal => signal.UnitPrice > 0).Select(signal => signal.UnitPrice).ToArray();

        return new PurchaseProfile(
            orderCount,
            categoryOrders,
            brandOrders,
            attributes,
            prices.Length > 0 ? prices.Average() : null,
            signals.Select(signal => signal.Product.Id).ToHashSet());
    }

    private static RecommendedProductDto? ScorePersonalized(Product product, PurchaseProfile profile)
    {
        var reasons = new List<RecommendationReasonDto>();
        var structuralScore = 0d;

        if (profile.CategoryOrderCounts.TryGetValue(product.CategoryId, out var categoryOrders))
        {
            structuralScore += 0.45d * categoryOrders / profile.OrderCount;
            reasons.Add(new("same_category", "Cùng danh mục bạn đã mua"));
        }

        if (product.BrandId is int brandId &&
            profile.BrandOrderCounts.TryGetValue(brandId, out var brandOrders))
        {
            structuralScore += 0.25d * brandOrders / profile.OrderCount;
            reasons.Add(new("same_brand", "Cùng thương hiệu bạn đã mua"));
        }

        var attributeAffinity = Jaccard(profile.AttributeKeys, AttributeKeys(product));
        if (attributeAffinity > 0)
        {
            structuralScore += 0.25d * attributeAffinity;
            reasons.Add(new("matching_attributes", "Có thuộc tính tương đồng với sản phẩm bạn đã mua"));
        }

        // Price is a refinement signal, not a standalone reason to recommend an unrelated product.
        if (structuralScore <= 0)
            return null;

        var priceAffinity = profile.AveragePrice is > 0
            ? Math.Max(0d, 1d - (double)(Math.Abs(product.Price - profile.AveragePrice.Value) /
                                        Math.Max(profile.AveragePrice.Value, 1m)))
            : 0d;
        if (priceAffinity > 0)
        {
            structuralScore += 0.05d * priceAffinity;
            reasons.Add(new("similar_price", "Mức giá gần với lịch sử mua hàng"));
        }

        return new RecommendedProductDto(product.ToDto(), structuralScore, reasons);
    }

    private static RecommendedProductDto? ScoreSimilar(
        Product target,
        IReadOnlySet<string> targetAttributes,
        Product candidate)
    {
        var reasons = new List<RecommendationReasonDto>();
        var score = 0d;

        if (candidate.CategoryId == target.CategoryId)
        {
            score += 0.45d;
            reasons.Add(new("same_category", "Cùng danh mục"));
        }

        if (target.BrandId.HasValue && candidate.BrandId == target.BrandId)
        {
            score += 0.25d;
            reasons.Add(new("same_brand", "Cùng thương hiệu"));
        }

        var attributeAffinity = Jaccard(targetAttributes, AttributeKeys(candidate));
        if (attributeAffinity > 0)
        {
            score += 0.25d * attributeAffinity;
            reasons.Add(new("matching_attributes", "Có thuộc tính tương đồng"));
        }

        // A price match alone must not make an otherwise unrelated product "similar".
        if (score <= 0)
            return null;

        var priceAffinity = Math.Max(0d, 1d - (double)(Math.Abs(candidate.Price - target.Price) /
                                                       Math.Max(target.Price, 1m)));
        if (priceAffinity > 0)
        {
            score += 0.05d * priceAffinity;
            reasons.Add(new("similar_price", "Mức giá gần nhau"));
        }

        return new RecommendedProductDto(candidate.ToDto(), score, reasons);
    }

    private static HashSet<string> AttributeKeys(Product product) =>
        product.AttributeValues
            .Where(value => value.AttributeDefinition is { IsActive: true, IsComparable: true })
            .Select(value =>
            {
                var raw = value.DataType switch
                {
                    CatalogAttributeType.Text => value.ValueText,
                    CatalogAttributeType.Number => value.ValueNumber?.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    CatalogAttributeType.Boolean => value.ValueBoolean?.ToString(),
                    _ => null
                };
                return string.IsNullOrWhiteSpace(raw)
                    ? null
                    : $"{value.AttributeDefinition.Code.Trim().ToLowerInvariant()}:{raw.Trim().ToLowerInvariant()}";
            })
            .Where(value => value is not null)
            .Select(value => value!)
            .ToHashSet(StringComparer.Ordinal);

    private static double Jaccard(IReadOnlySet<string> left, IReadOnlySet<string> right)
    {
        if (left.Count == 0 || right.Count == 0)
            return 0;

        var intersection = left.Count(right.Contains);
        var union = left.Count + right.Count - intersection;
        return union == 0 ? 0 : (double)intersection / union;
    }

    private static RecommendationResponseDto Response(
        string strategy,
        IReadOnlyList<RecommendedProductDto> items) =>
        new(strategy, DateTime.UtcNow, items);

    private sealed record PurchaseSignal(int OrderId, Product Product, decimal UnitPrice, int Quantity);

    private sealed record PurchaseProfile(
        int OrderCount,
        IReadOnlyDictionary<int, int> CategoryOrderCounts,
        IReadOnlyDictionary<int, int> BrandOrderCounts,
        IReadOnlySet<string> AttributeKeys,
        decimal? AveragePrice,
        HashSet<int> PurchasedProductIds)
    {
        public static PurchaseProfile Empty { get; } = new(
            0,
            new Dictionary<int, int>(),
            new Dictionary<int, int>(),
            new HashSet<string>(),
            null,
            []);
    }
}
