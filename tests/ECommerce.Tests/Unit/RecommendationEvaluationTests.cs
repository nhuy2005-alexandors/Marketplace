using ECommerce.Application.Options;
using ECommerce.Application.Services;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit.Abstractions;

namespace ECommerce.Tests.Unit;

/// <summary>Small deterministic temporal benchmark. Labels are synthetic author-written engineering fixtures.</summary>
public sealed class RecommendationEvaluationTests(ITestOutputHelper output)
{
    private static readonly DateTime TrainCutoff = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime HoldoutCutoff = new(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task TemporalSyntheticBenchmark_ReportsMetricsWithoutTuningOnHoldout()
    {
        using var db = TestDb.Create();
        var products = new[]
        {
            Product(10, "Compact battery phone", 1, 1, 100m),
            Product(20, "Large screen phone", 2, 1, 100m),
            Product(30, "Compact battery mobile", 1, 1, 300m),
            Product(40, "Compact battery mobile plus", 1, 1, 300m)
        };
        db.Users.Add(new User { Id = 1, Email = "eval-seller@example.test", PasswordHash = "test", FullName = "Seller", Role = UserRole.Seller, ShopName = "Evaluation shop" });
        db.Users.AddRange(Enumerable.Range(100, 5).Select(id => new User
        {
            Id = id, Email = $"eval-{id}@example.test", PasswordHash = "test", FullName = $"Synthetic user {id}",
            Role = UserRole.Customer, CreatedAt = TrainCutoff.AddDays(-30)
        }));
        db.Categories.Add(new Category { Id = 1, Name = "Synthetic Phones", CreatedAt = TrainCutoff.AddDays(-60) });
        db.Brands.AddRange(
            new Brand { Id = 1, Name = "Brand One", Slug = "brand-one" },
            new Brand { Id = 2, Name = "Brand Two", Slug = "brand-two" });
        db.AttributeDefinitions.Add(new AttributeDefinition
        {
            Id = 1, CategoryId = 1, Code = "battery", DisplayName = "Battery", DataType = CatalogAttributeType.Number,
            Unit = "mAh", IsActive = true, IsComparable = true, IsFilterable = true
        });
        db.Products.AddRange(products);
        db.ProductAttributeValues.AddRange(products.Select(product => new ProductAttributeValue
        {
            ProductId = product.Id, AttributeDefinitionId = 1, CategoryId = 1, DataType = CatalogAttributeType.Number,
            ValueNumber = product.Id == 20 ? 8m : 16m
        }));

        var orderId = 1000;
        var itemId = 1000;
        foreach (var userId in Enumerable.Range(100, 5))
        {
            db.Orders.Add(Order(orderId++, userId, products[0], TrainCutoff.AddDays(-10), 100m, itemId++));
            db.Orders.Add(Order(orderId++, userId, products[2], TrainCutoff.AddDays(10), 300m, itemId++));
            db.Orders.Add(Order(orderId++, userId, products[3], HoldoutCutoff.AddDays(10), 300m, itemId++));
        }
        await db.SaveChangesAsync();

        var enrichedOptions = new RecommendationOptions
        {
            CategoryWeight = 0.4, PriceWeight = 0.2, BrandWeight = 0.15, AttributeWeight = 0.2,
            CharacteristicWeight = 0.05, RepeatPurchaseWeight = 0, PersonalizedMinimumScore = 0.01,
            SimilarCategoryWeight = 0.4, SimilarPriceWeight = 0.2, SimilarBrandWeight = 0.15,
            SimilarAttributeWeight = 0.2, SimilarCharacteristicWeight = 0.05, SimilarMinimumScore = 0.01,
            MaximumProductsPerCategory = 20, AllowRePurchase = false
        };
        var baselineOptions = new RecommendationOptions
        {
            CategoryWeight = 0.6, PriceWeight = 0.4, BrandWeight = 0, AttributeWeight = 0,
            CharacteristicWeight = 0, RepeatPurchaseWeight = 0, PersonalizedMinimumScore = 0.01,
            SimilarCategoryWeight = 0.6, SimilarPriceWeight = 0.4, SimilarBrandWeight = 0,
            SimilarAttributeWeight = 0, SimilarCharacteristicWeight = 0, SimilarMinimumScore = 0.01,
            MaximumProductsPerCategory = 20, AllowRePurchase = false
        };

        var developmentLabels = Enumerable.Range(100, 5).ToDictionary(id => id, _ => new HashSet<int> { 30 });
        var holdoutLabels = Enumerable.Range(100, 5).ToDictionary(id => id, _ => new HashSet<int> { 40 });
        var developmentBaseline = await PersonalizedAsync(db, baselineOptions, developmentLabels, TrainCutoff);
        var developmentEnriched = await PersonalizedAsync(db, enrichedOptions, developmentLabels, TrainCutoff);
        var holdoutBaseline = await PersonalizedAsync(db, baselineOptions, holdoutLabels, HoldoutCutoff);
        var holdoutEnriched = await PersonalizedAsync(db, enrichedOptions, holdoutLabels, HoldoutCutoff);

        WriteMetrics("development", "baseline", developmentBaseline);
        WriteMetrics("development", "Catalog V2 features", developmentEnriched);
        WriteMetrics("holdout", "baseline", holdoutBaseline);
        WriteMetrics("holdout", "Catalog V2 features", holdoutEnriched);

        var similarBaseline = await new RecommendationService(db, Options.Create(baselineOptions))
            .GetSimilarAsync(10, 3, HoldoutCutoff, HoldoutCutoff);
        var similarEnriched = await new RecommendationService(db, Options.Create(enrichedOptions))
            .GetSimilarAsync(10, 3, HoldoutCutoff, HoldoutCutoff);
        var similarLabels = new HashSet<int> { 30, 40 };
        WriteMetrics("similar-product", "baseline", [new MetricRow(similarBaseline.Items.Select(item => item.Product.Id).ToArray(), similarLabels)]);
        WriteMetrics("similar-product", "Catalog V2 features", [new MetricRow(similarEnriched.Items.Select(item => item.Product.Id).ToArray(), similarLabels)]);

        var coldStart = await new RecommendationService(db, Options.Create(enrichedOptions))
            .GetForUserAsync(999, 4, HoldoutCutoff, HoldoutCutoff);
        coldStart.Strategy.Should().Be("popular");
        output.WriteLine("cold-start: user-without-history strategy={0}; visible={1}", coldStart.Strategy, coldStart.Items.Count);

        var rfm = await new RfmService(db).GetCustomersAsync(1, 20, HoldoutCutoff, HoldoutCutoff);
        var rfmScores = rfm.Items.ToDictionary(item => item.UserId);
        var segmentMetrics = holdoutEnriched.GroupBy(row => rfmScores[row.UserId].SegmentCode)
            .ToDictionary(group => group.Key, group => Metrics(group.ToArray(), 3));
        output.WriteLine("RFM segments (synthetic holdout; one-user-or-small buckets): {0}",
            string.Join("; ", segmentMetrics.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key} n={holdoutEnriched.Count(row => rfmScores[row.UserId].SegmentCode == pair.Key)} P@3={pair.Value.PrecisionAtK:P1} R@3={pair.Value.RecallAtK:P1} NDCG@3={pair.Value.NdcgAtK:P1}")));

        developmentEnriched.Should().OnlyContain(row => row.RankedIds[0] == 30);
        holdoutEnriched.Should().OnlyContain(row => row.RankedIds[0] == 40);
        similarEnriched.Items.Take(2).Select(item => item.Product.Id).Should().BeSubsetOf(similarLabels);
        similarEnriched.Items.Single(item => item.Product.Id == 30).Reasons.Select(reason => reason.Code)
            .Should().Contain("similar_characteristics");
        var personalizedReasons = await new RecommendationService(db, Options.Create(enrichedOptions))
            .GetForUserAsync(100, 3, TrainCutoff, TrainCutoff);
        personalizedReasons.Items.Single(item => item.Product.Id == 30).Reasons.Select(reason => reason.Code)
            .Should().Contain("similar_characteristics");
    }

    private async Task<List<MetricRow>> PersonalizedAsync(ECommerce.Infrastructure.Persistence.AppDbContext db,
        RecommendationOptions options, IReadOnlyDictionary<int, HashSet<int>> labels, DateTime cutoff)
    {
        var service = new RecommendationService(db, Options.Create(options));
        var rows = new List<MetricRow>();
        foreach (var (userId, expected) in labels)
        {
            var result = await service.GetForUserAsync(userId, 3, cutoff, cutoff);
            rows.Add(new MetricRow(result.Items.Select(item => item.Product.Id).ToArray(), expected, userId));
        }
        return rows;
    }

    private void WriteMetrics(string split, string label, IReadOnlyList<MetricRow> rows)
    {
        var metrics = Metrics(rows, 3);
        var topOne = Metrics(rows, 1);
        output.WriteLine("{0} {1}: users={2} P@1={3:P1} P@3={4:P1} R@3={5:P1} NDCG@3={6:P1} MRR@3={7:P1}",
            split, label, rows.Count, topOne.PrecisionAtK, metrics.PrecisionAtK, metrics.RecallAtK, metrics.NdcgAtK, metrics.MrrAtK);
    }

    private static MetricsResult Metrics(IReadOnlyList<MetricRow> rows, int k)
    {
        var precision = rows.Average(row => row.RankedIds.Take(k).Count(row.ExpectedIds.Contains) / (double)k);
        var recall = rows.Average(row => row.ExpectedIds.Count == 0 ? 0 : row.RankedIds.Take(k).Count(row.ExpectedIds.Contains) / (double)row.ExpectedIds.Count);
        var ndcg = rows.Average(row =>
        {
            var dcg = row.RankedIds.Take(k).Select((id, index) => row.ExpectedIds.Contains(id) ? 1d / Math.Log2(index + 2) : 0d).Sum();
            var ideal = Enumerable.Range(0, Math.Min(k, row.ExpectedIds.Count)).Select(index => 1d / Math.Log2(index + 2)).Sum();
            return ideal == 0 ? 0 : dcg / ideal;
        });
        var mrr = rows.Average(row =>
        {
            var index = Array.FindIndex(row.RankedIds.Take(k).ToArray(), row.ExpectedIds.Contains);
            return index < 0 ? 0 : 1d / (index + 1);
        });
        return new MetricsResult(precision, recall, ndcg, mrr);
    }

    private static Product Product(int id, string name, int brandId, int categoryId, decimal price) => new()
    {
        Id = id, Name = name, Description = name, Sku = $"SYN-{id}", BrandId = brandId, CategoryId = categoryId,
        SellerId = 1, Price = price, Stock = 5, CreatedAt = TrainCutoff.AddDays(-90)
    };

    private static Order Order(int id, int userId, Product product, DateTime createdAt, decimal amount, int itemId) => new()
    {
        Id = id, UserId = userId, Status = OrderStatus.Delivered, ShippingAddress = "Synthetic fixture",
        CreatedAt = createdAt, Items = [new OrderItem
        {
            Id = itemId, ProductId = product.Id, Product = product, ProductName = product.Name, UnitPrice = product.Price,
            Quantity = 1, Status = FulfillmentStatus.Delivered, CreatedAt = createdAt
        }],
        Payment = new Payment { Id = id, OrderId = id, Amount = amount, Status = PaymentStatus.Completed, CreatedAt = createdAt }
    };

    private sealed record MetricRow(int[] RankedIds, HashSet<int> ExpectedIds, int UserId = 0);
    private sealed record MetricsResult(double PrecisionAtK, double RecallAtK, double NdcgAtK, double MrrAtK);
}
