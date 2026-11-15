using ECommerce.Application;
using ECommerce.Application.Interfaces;
using ECommerce.Application.Options;
using ECommerce.Application.Services;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using ECommerce.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace ECommerce.Tests.Unit;

public sealed class RecommendationServiceTests
{
    private static readonly DateTime Cutoff = new(2026, 11, 15, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(OrderStatus.Pending)]
    [InlineData(OrderStatus.Cancelled)]
    public async Task GetForUserAsync_ExcludesIneligibleParentOrderStatuses(OrderStatus status)
    {
        using var db = TestDb.Create();
        var purchased = Product(10, categoryId: 1);
        var candidate = Product(11, categoryId: 1);
        var irrelevant = Product(12, categoryId: 2);
        await SeedProductsAsync(db, purchased, candidate, irrelevant);
        db.Orders.Add(Order(1, 7, status, PaymentStatus.Completed, [Item(1, purchased)]));
        await db.SaveChangesAsync();

        var result = await Service(db, new RecommendationOptions
        {
            CategoryWeight = 1,
            PriceWeight = 0,
            RepeatPurchaseWeight = 0,
            PersonalizedMinimumScore = 0.1
        }).GetForUserAsync(7, asOfUtc: Cutoff, cutoffExclusiveUtc: Cutoff);

        result.Strategy.Should().Be("no_signal");
        result.Items.Should().NotContain(item => item.Product.Id == candidate.Id);
        result.Items.Should().NotContain(item => item.Product.Id == irrelevant.Id);
    }

    [Theory]
    [InlineData(PaymentStatus.Pending)]
    [InlineData(PaymentStatus.Failed)]
    [InlineData(PaymentStatus.Refunded)]
    public async Task GetForUserAsync_RequiresCompletedPayment(PaymentStatus paymentStatus)
    {
        using var db = TestDb.Create();
        var purchased = Product(20, categoryId: 1);
        var candidate = Product(21, categoryId: 1);
        await SeedProductsAsync(db, purchased, candidate);
        db.Orders.Add(Order(2, 8, OrderStatus.Paid, paymentStatus, [Item(2, purchased)]));
        await db.SaveChangesAsync();

        var result = await Service(db, new RecommendationOptions
        {
            CategoryWeight = 1,
            PriceWeight = 0,
            RepeatPurchaseWeight = 0,
            PersonalizedMinimumScore = 0.1
        }).GetForUserAsync(8, asOfUtc: Cutoff, cutoffExclusiveUtc: Cutoff);

        result.Strategy.Should().Be("no_signal");
        result.Items.Should().NotContain(item => item.Product.Id == candidate.Id);
    }

    [Fact]
    public async Task GetForUserAsync_CancelledItemsDoNotContributeOrTriggerRepurchase()
    {
        using var db = TestDb.Create();
        var purchasedAndCancelled = Product(30, categoryId: 1);
        var sameCategoryCandidate = Product(31, categoryId: 1);
        await SeedProductsAsync(db, purchasedAndCancelled, sameCategoryCandidate);
        db.Orders.Add(Order(3, 9, OrderStatus.Delivered, PaymentStatus.Completed,
        [
            Item(3, purchasedAndCancelled, FulfillmentStatus.Cancelled)
        ]));
        await db.SaveChangesAsync();

        var result = await Service(db, new RecommendationOptions
        {
            CategoryWeight = 1,
            PriceWeight = 0,
            RepeatPurchaseWeight = 1,
            PersonalizedMinimumScore = 0.1
        }).GetForUserAsync(9, asOfUtc: Cutoff, cutoffExclusiveUtc: Cutoff);

        result.Strategy.Should().Be("no_signal");
        result.Items.Should().NotContain(item => item.Product.Id == purchasedAndCancelled.Id);
        result.Items.Should().NotContain(item => item.Product.Id == sameCategoryCandidate.Id);
    }

    [Fact]
    public void BuildPurchaseProfile_CountsDistinctOrdersAndPaymentAmountOncePerOrder()
    {
        var product = Product(40, categoryId: 4);
        var first = Order(4, 10, OrderStatus.Paid, PaymentStatus.Completed,
        [Item(4, product, quantity: 2), Item(5, product, quantity: 1)], amount: 27m);
        var second = Order(5, 10, OrderStatus.Delivered, PaymentStatus.Completed,
            [Item(6, product)], amount: 13m);

        var profile = RecommendationService.BuildPurchaseProfile([first, first, second]);

        profile.Frequency.Should().Be(2);
        profile.Monetary.Should().Be(40m);
        profile.CategoryOrderCounts[4].Should().Be(2);
        profile.ProductQuantities[product.Id].Should().Be(4);
    }

    [Fact]
    public async Task GetSimilarAsync_ExcludesTargetOutOfStockAndZeroScoreCandidates()
    {
        using var db = TestDb.Create();
        var target = Product(50, categoryId: 1, price: 100m);
        var relevant = Product(51, categoryId: 1, price: 100m);
        var unrelated = Product(52, categoryId: 2, price: 1000m);
        var outOfStock = Product(53, categoryId: 1, price: 100m, stock: 0);
        await SeedProductsAsync(db, target, relevant, unrelated, outOfStock);
        await db.SaveChangesAsync();

        var result = await Service(db, new RecommendationOptions
        {
            SimilarCategoryWeight = 0.6,
            SimilarPriceWeight = 0.4,
            SimilarMinimumScore = 0.5,
            MaximumProductsPerCategory = 2
        }).GetSimilarAsync(target.Id, asOfUtc: Cutoff, cutoffExclusiveUtc: Cutoff);

        result.Items.Select(item => item.Product.Id).Should().Equal(relevant.Id);
        result.Items.Should().OnlyContain(item => item.Score > 0 && item.Reasons.Count > 0);
        result.Items.Single().Reasons.Select(reason => reason.Code).Should().Contain("same_category");
    }

    [Fact]
    public async Task GetSimilarAsync_UsesProductIdTieBreakAndCategoryDiversityCap()
    {
        using var db = TestDb.Create();
        var target = Product(54, categoryId: 9, price: 200m);
        var lowerId = Product(55, categoryId: 1, price: 100m);
        var higherId = Product(56, categoryId: 1, price: 100m);
        var nextCategory = Product(57, categoryId: 2, price: 100m);
        await SeedProductsAsync(db, target, higherId, nextCategory, lowerId);
        await db.SaveChangesAsync();

        var result = await Service(db, new RecommendationOptions
        {
            SimilarCategoryWeight = 0.6,
            SimilarPriceWeight = 0.4,
            SimilarMinimumScore = 0.15,
            MaximumRelativePriceDifference = 0.6m,
            MaximumProductsPerCategory = 1
        }).GetSimilarAsync(target.Id, take: 3, asOfUtc: Cutoff, cutoffExclusiveUtc: Cutoff);

        result.Items.Select(item => item.Product.Id).Should().Equal(lowerId.Id, nextCategory.Id);
    }

    [Fact]
    public async Task GetForUserAsync_AllowsRepurchaseOnlyWhenPolicyEnablesItAndLabelsContribution()
    {
        using var db = TestDb.Create();
        var purchased = Product(60, categoryId: 1, price: 50m);
        await SeedProductsAsync(db, purchased);
        db.Orders.Add(Order(6, 11, OrderStatus.Paid, PaymentStatus.Completed, [Item(7, purchased)], amount: 50m));
        await db.SaveChangesAsync();

        var allowed = await Service(db, new RecommendationOptions
        {
            AllowRePurchase = true,
            CategoryWeight = 0,
            PriceWeight = 0,
            RepeatPurchaseWeight = 0.5,
            PersonalizedMinimumScore = 0.25
        }).GetForUserAsync(11, asOfUtc: Cutoff, cutoffExclusiveUtc: Cutoff);

        allowed.Strategy.Should().Be("personalized");
        allowed.Items.Should().ContainSingle(item => item.Product.Id == purchased.Id);
        allowed.Items.Single().Reasons.Should().Contain(reason => reason.Code == "repeat_purchase");

        var blocked = await Service(db, new RecommendationOptions
        {
            AllowRePurchase = false,
            CategoryWeight = 0,
            PriceWeight = 0,
            RepeatPurchaseWeight = 0.5
        }).GetForUserAsync(11, asOfUtc: Cutoff, cutoffExclusiveUtc: Cutoff);

        blocked.Items.Should().NotContain(item => item.Product.Id == purchased.Id);
    }

    [Fact]
    public async Task GetForUserAsync_UsesTruthfulGuestFallbackAndReturnsEmptyForEmptyCatalog()
    {
        using var db = TestDb.Create();
        var popular = Product(70, categoryId: 1);
        var rated = Product(71, categoryId: 2);
        rated.Reviews.Add(new Review { Id = 1, ProductId = rated.Id, UserId = 1, Rating = 5, CreatedAt = Cutoff.AddDays(-1) });
        await SeedProductsAsync(db, popular, rated);
        db.Orders.Add(Order(7, 12, OrderStatus.Delivered, PaymentStatus.Completed, [Item(8, popular)]));
        await db.SaveChangesAsync();

        var guestResult = await Service(db, new RecommendationOptions())
            .GetForUserAsync(null, asOfUtc: Cutoff, cutoffExclusiveUtc: Cutoff);
        guestResult.Strategy.Should().Be("popular");
        guestResult.Items.Should().ContainSingle(item => item.Product.Id == popular.Id);
        guestResult.Items.Single().Reasons.Should().Contain(reason => reason.Code == "popular");

        var noHistory = await Service(db, new RecommendationOptions())
            .GetForUserAsync(999, asOfUtc: Cutoff, cutoffExclusiveUtc: Cutoff);
        noHistory.Strategy.Should().Be("popular");
        noHistory.Items.Should().Contain(item => item.Product.Id == popular.Id);

        using var emptyDb = TestDb.Create();
        var empty = await Service(emptyDb, new RecommendationOptions())
            .GetForUserAsync(null, asOfUtc: Cutoff, cutoffExclusiveUtc: Cutoff);
        empty.Strategy.Should().Be("empty_catalog");
        empty.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetForUserAsync_UsesTopRatedFallbackWhenNoEligibleSalesExist()
    {
        using var db = TestDb.Create();
        var rated = Product(80, categoryId: 1);
        rated.Reviews.Add(new Review { Id = 2, ProductId = rated.Id, UserId = 1, Rating = 4, CreatedAt = Cutoff.AddDays(-1) });
        await SeedProductsAsync(db, rated);
        await db.SaveChangesAsync();

        var result = await Service(db, new RecommendationOptions())
            .GetForUserAsync(null, asOfUtc: Cutoff, cutoffExclusiveUtc: Cutoff);

        result.Strategy.Should().Be("top_rated");
        result.Items.Should().ContainSingle(item => item.Product.Id == rated.Id);
        result.Items.Single().Reasons.Should().Contain(reason => reason.Code == "top_rated");
    }

    [Fact]
    public async Task GetForUserAsync_AppliesExclusiveCutoffToCatalogOrdersPaymentsItemsAndReviews()
    {
        using var db = TestDb.Create();
        var candidate = Product(90, categoryId: 1);
        var productAtCutoff = Product(91, categoryId: 1);
        productAtCutoff.CreatedAt = Cutoff;
        candidate.Reviews.Add(new Review { Id = 3, ProductId = candidate.Id, UserId = 1, Rating = 5, CreatedAt = Cutoff });
        await SeedProductsAsync(db, candidate, productAtCutoff);

        var paymentAtCutoff = Order(9, 13, OrderStatus.Delivered, PaymentStatus.Completed, [Item(10, candidate)]);
        paymentAtCutoff.Payment!.CreatedAt = Cutoff;
        db.Orders.Add(paymentAtCutoff);
        var itemAtCutoff = Item(11, candidate);
        itemAtCutoff.CreatedAt = Cutoff;
        db.Orders.Add(Order(10, 13, OrderStatus.Delivered, PaymentStatus.Completed, [itemAtCutoff]));
        var orderAtCutoff = Order(12, 13, OrderStatus.Delivered, PaymentStatus.Completed, [Item(13, candidate)]);
        orderAtCutoff.CreatedAt = Cutoff;
        db.Orders.Add(orderAtCutoff);
        await db.SaveChangesAsync();

        var result = await Service(db, new RecommendationOptions())
            .GetForUserAsync(13, asOfUtc: Cutoff, cutoffExclusiveUtc: Cutoff);

        result.Strategy.Should().Be("no_signal");
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetForUserAsync_OneEligibleOrderUsesPersonalizedSignals()
    {
        using var db = TestDb.Create();
        var purchased = Product(92, categoryId: 3, price: 100m);
        var candidate = Product(93, categoryId: 3, price: 100m);
        await SeedProductsAsync(db, purchased, candidate);
        db.Orders.Add(Order(11, 14, OrderStatus.Paid, PaymentStatus.Completed, [Item(12, purchased)]));
        await db.SaveChangesAsync();

        var result = await Service(db, new RecommendationOptions
        {
            CategoryWeight = 1,
            PriceWeight = 0,
            RepeatPurchaseWeight = 0,
            PersonalizedMinimumScore = 0.5
        }).GetForUserAsync(14, asOfUtc: Cutoff, cutoffExclusiveUtc: Cutoff);

        result.Strategy.Should().Be("personalized");
        result.Items.Should().Contain(item => item.Product.Id == candidate.Id);
        result.Items.Single(item => item.Product.Id == candidate.Id)
            .Reasons.Should().Contain(reason => reason.Code == "same_category");
    }

    [Fact]
    public void AddApplication_RegistersRecommendationServiceAsScoped()
    {
        var services = new ServiceCollection();
        services.AddApplication();

        var registration = services.Single(descriptor => descriptor.ServiceType == typeof(IRecommendationService));
        registration.Lifetime.Should().Be(ServiceLifetime.Scoped);
        registration.ImplementationType.Should().Be(typeof(RecommendationService));

        var rfmRegistration = services.Single(descriptor => descriptor.ServiceType == typeof(IRfmService));
        rfmRegistration.Lifetime.Should().Be(ServiceLifetime.Scoped);
        rfmRegistration.ImplementationType.Should().Be(typeof(RfmService));
    }

    private static RecommendationService Service(
        IAppDbContext db,
        RecommendationOptions options) =>
        new(db, Options.Create(options));

    private static async Task SeedProductsAsync(AppDbContext db, params Product[] products)
    {
        db.Users.Add(new User
        {
            Id = 1,
            Email = "recommendation-seller@example.test",
            PasswordHash = "test-only",
            FullName = "Test Seller",
            Role = UserRole.Seller,
            ShopName = "Test Shop"
        });
        db.Categories.AddRange(products.Select(product => product.CategoryId).Distinct()
            .Select(categoryId => new Category
            {
                Id = categoryId,
                Name = $"Category {categoryId}",
                CreatedAt = Cutoff.AddDays(-20)
            }));
        db.Products.AddRange(products);
        await db.SaveChangesAsync();
    }

    private static Product Product(int id, int categoryId, decimal price = 100m, int stock = 5) => new()
    {
        Id = id,
        Name = $"Product {id}",
        CategoryId = categoryId,
        SellerId = 1,
        Price = price,
        Stock = stock,
        CreatedAt = Cutoff.AddDays(-10)
    };

    private static OrderItem Item(
        int id,
        Product product,
        FulfillmentStatus status = FulfillmentStatus.Delivered,
        int quantity = 1) => new()
    {
        Id = id,
        ProductId = product.Id,
        Product = product,
        ProductName = product.Name,
        UnitPrice = product.Price,
        Quantity = quantity,
        Status = status,
        CreatedAt = Cutoff.AddDays(-3)
    };

    private static Order Order(
        int id,
        int userId,
        OrderStatus orderStatus,
        PaymentStatus paymentStatus,
        List<OrderItem> items,
        decimal amount = 100m) => new()
    {
        Id = id,
        UserId = userId,
        Status = orderStatus,
        ShippingAddress = "test",
        CreatedAt = Cutoff.AddDays(-5),
        Items = items,
        Payment = new Payment
        {
            Id = id,
            OrderId = id,
            Amount = amount,
            Status = paymentStatus,
            CreatedAt = Cutoff.AddDays(-5)
        }
    };
}
