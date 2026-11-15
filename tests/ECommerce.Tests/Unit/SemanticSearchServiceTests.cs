using ECommerce.Application.DTOs.AI;
using ECommerce.Application.DTOs.Catalog;
using ECommerce.Application.Interfaces;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using ECommerce.Infrastructure.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace ECommerce.Tests.Unit;

public sealed class SemanticSearchServiceTests
{
    [Fact]
    public void EmbeddingCacheStore_EvictsOldestEntriesWhenCapacityIsExceeded()
    {
        var cache = new EmbeddingCacheStore();

        for (var index = 0; index <= 1000; index++)
            cache.Set($"hash-{index}", [index]);

        Assert.Equal(1000, cache.Count);
        Assert.False(cache.TryGet("hash-0", out _));
        Assert.True(cache.TryGet("hash-1000", out var newest));
        Assert.Equal(1000f, newest[0]);
    }

    [Fact]
    public async Task SearchCandidatesAsync_UsesLexicalFallbackAndExcludesUnapprovedSellers()
    {
        using var db = TestDb.Create();
        var category = new Category { Id = 1, Name = "Audio" };
        var approved = Seller(2, SellerStatus.Approved);
        var pending = Seller(3, SellerStatus.Pending);
        var visible = Product(10, "Wireless headphones", "Bluetooth over-ear headset", category, approved);
        var hidden = Product(11, "Wireless headphones", "Bluetooth over-ear headset", category, pending);
        db.Users.AddRange(approved, pending);
        db.Categories.Add(category);
        db.Products.AddRange(visible, hidden);
        await db.SaveChangesAsync();

        var service = Service(db, new FakeEmbeddings(returnNull: true), new EmbeddingCacheStore());

        var results = await service.SearchCandidatesAsync("wireless headphones", null, 8);

        var result = Assert.Single(results);
        Assert.Equal(visible.Id, result.Product.Id);
        Assert.Equal("lexical", service.RetrievalMode);
        Assert.True(result.LexicalScore > 0);
        Assert.Equal(0, result.SemanticScore);
    }

    [Fact]
    public async Task SearchCandidatesAsync_UsesSemanticMatchWhenLexicalOverlapIsAbsent()
    {
        using var db = TestDb.Create();
        var category = new Category { Id = 1, Name = "Electronics" };
        var seller = Seller(2, SellerStatus.Approved);
        var product = Product(10, "Aurora audio kit", "Portable sound device", category, seller);
        db.Users.Add(seller);
        db.Categories.Add(category);
        db.Products.Add(product);
        await db.SaveChangesAsync();

        var service = Service(db, new FakeEmbeddings(), new EmbeddingCacheStore());

        var results = await service.SearchCandidatesAsync("noise cancelling", null, 8);

        var result = Assert.Single(results);
        Assert.Equal(product.Id, result.Product.Id);
        Assert.Equal(0, result.LexicalScore);
        Assert.Equal(1d, result.SemanticScore, 5);
        Assert.Equal(0.65d, result.Score, 5);
        Assert.Equal("hybrid", service.RetrievalMode);
    }

    [Fact]
    public async Task SearchCandidatesAsync_AppliesCatalogAndTypedAttributeFilters()
    {
        using var db = TestDb.Create();
        var category = new Category { Id = 1, Name = "Audio" };
        var seller = Seller(2, SellerStatus.Approved);
        var color = new AttributeDefinition
        {
            Id = 1,
            CategoryId = category.Id,
            Category = category,
            Code = "color",
            DisplayName = "Color",
            DataType = CatalogAttributeType.Text,
            IsFilterable = true,
            IsComparable = true,
            IsActive = true
        };
        var black = Product(10, "Studio headphones", "Over-ear audio", category, seller);
        black.AttributeValues.Add(new ProductAttributeValue
        {
            ProductId = black.Id,
            Product = black,
            AttributeDefinitionId = color.Id,
            AttributeDefinition = color,
            CategoryId = category.Id,
            DataType = CatalogAttributeType.Text,
            ValueText = "black"
        });
        var white = Product(11, "Studio headphones", "Over-ear audio", category, seller);
        white.AttributeValues.Add(new ProductAttributeValue
        {
            ProductId = white.Id,
            Product = white,
            AttributeDefinitionId = color.Id,
            AttributeDefinition = color,
            CategoryId = category.Id,
            DataType = CatalogAttributeType.Text,
            ValueText = "white"
        });
        db.Users.Add(seller);
        db.Categories.Add(category);
        db.AttributeDefinitions.Add(color);
        db.Products.AddRange(black, white);
        await db.SaveChangesAsync();

        var service = Service(db, new FakeEmbeddings(returnNull: true), new EmbeddingCacheStore());
        var filters = new AssistantChatFilters(
            CategoryId: category.Id,
            MinPrice: 50m,
            MaxPrice: 100m,
            InStockOnly: true,
            AttributeFilters: [new ProductAttributeFilter(color.Id, ValueText: "black")]);

        var results = await service.SearchCandidatesAsync("studio headphones", filters, 8);

        var result = Assert.Single(results);
        Assert.Equal(black.Id, result.Product.Id);
        Assert.DoesNotContain(results, candidate => candidate.Product.Id == white.Id);
    }

    [Fact]
    public async Task SearchCandidatesAsync_RejectsInvalidFilterBeforeCallingEmbeddingProvider()
    {
        using var db = TestDb.Create();
        var embeddings = new FakeEmbeddings();
        var service = Service(db, embeddings, new EmbeddingCacheStore());

        var results = await service.SearchCandidatesAsync(
            "headphones",
            new AssistantChatFilters(MinPrice: 200m, MaxPrice: 100m),
            8);

        Assert.Empty(results);
        Assert.Equal(0, embeddings.CallCount);
    }

    [Fact]
    public async Task SearchCandidatesAsync_IncludesProductsBeyondFirstFiveHundredCatalogRows()
    {
        using var db = TestDb.Create();
        var category = new Category { Id = 1, Name = "Audio" };
        var seller = Seller(2, SellerStatus.Approved);
        db.Users.Add(seller);
        db.Categories.Add(category);
        db.Products.AddRange(Enumerable.Range(1, 501)
            .Select(id => Product(
                id,
                id == 501 ? "Target camera" : $"Generic audio item {id}",
                "Portable sound device",
                category,
                seller)));
        await db.SaveChangesAsync();

        var service = Service(db, new FakeEmbeddings(returnNull: true), new EmbeddingCacheStore());

        var results = await service.SearchCandidatesAsync("target camera", null, 8);

        var result = Assert.Single(results);
        Assert.Equal(501, result.Product.Id);
    }

    [Fact]
    public async Task WarmUpAsync_BoundsLongDocumentsAndPreservesProductIdentity()
    {
        using var db = TestDb.Create();
        var category = new Category { Id = 1, Name = "Audio" };
        var seller = Seller(2, SellerStatus.Approved);
        var product = Product(10, "Long description product", new string('x', 5000), category, seller);
        db.Users.Add(seller);
        db.Categories.Add(category);
        db.Products.Add(product);
        await db.SaveChangesAsync();

        var embeddings = new FakeEmbeddings();
        var service = Service(db, embeddings, new EmbeddingCacheStore());

        await service.WarmUpAsync();

        var document = Assert.Single(embeddings.EmbeddedTexts);
        Assert.True(document.Length <= 1500);
        Assert.Contains("Long description product", document);
        Assert.Contains("Audio", document);
        Assert.Contains("SKU-10", document);
    }

    [Fact]
    public async Task WarmUpAsync_ContinuesWithLaterBatchesWhenOneBatchFails()
    {
        using var db = TestDb.Create();
        var category = new Category { Id = 1, Name = "Audio" };
        var seller = Seller(2, SellerStatus.Approved);
        db.Users.Add(seller);
        db.Categories.Add(category);
        db.Products.AddRange(Enumerable.Range(1, 65)
            .Select(id => Product(id, $"Audio product {id}", "Portable sound device", category, seller)));
        await db.SaveChangesAsync();

        var cache = new EmbeddingCacheStore();
        var embeddings = new FakeEmbeddings(failOnCall: 2);
        var service = Service(db, embeddings, cache);

        await service.WarmUpAsync();

        Assert.Equal(33, cache.Count);
        Assert.Equal(3, embeddings.CallCount);
    }

    [Fact]
    public async Task WarmUpAsync_CachesProductEmbeddingsForSubsequentHybridRetrieval()
    {
        using var db = TestDb.Create();
        var category = new Category { Id = 1, Name = "Audio" };
        var seller = Seller(2, SellerStatus.Approved);
        var product = Product(10, "Aurora audio kit", "Portable sound device", category, seller);
        db.Users.Add(seller);
        db.Categories.Add(category);
        db.Products.Add(product);
        await db.SaveChangesAsync();

        var cache = new EmbeddingCacheStore();
        var embeddings = new FakeEmbeddings();
        var service = Service(db, embeddings, cache);

        await service.WarmUpAsync();
        var results = await service.SearchCandidatesAsync("noise cancelling", null, 8);

        Assert.Equal(1, cache.Count);
        Assert.Equal("hybrid", service.RetrievalMode);
        Assert.Single(results);
        Assert.Equal(2, embeddings.CallCount);
        Assert.Contains("RETRIEVAL_DOCUMENT", embeddings.TaskTypes);
        Assert.Contains("RETRIEVAL_QUERY", embeddings.TaskTypes);
    }

    private static SemanticSearchService Service(
        ECommerce.Infrastructure.Persistence.AppDbContext db,
        FakeEmbeddings embeddings,
        EmbeddingCacheStore cache) =>
        new(db, embeddings, cache, NullLogger<SemanticSearchService>.Instance);

    private static User Seller(int id, SellerStatus status) => new()
    {
        Id = id,
        Email = $"seller{id}@example.test",
        PasswordHash = "test",
        FullName = $"Seller {id}",
        ShopName = $"Shop {id}",
        Role = UserRole.Seller,
        SellerStatus = status
    };

    private static Product Product(
        int id,
        string name,
        string description,
        Category category,
        User seller) => new()
    {
        Id = id,
        Name = name,
        Sku = $"SKU-{id}",
        Description = description,
        Price = 89m,
        Stock = 5,
        CategoryId = category.Id,
        Category = category,
        SellerId = seller.Id,
        Seller = seller,
        Reviews = [],
        AttributeValues = []
    };

    private sealed class FakeEmbeddings(bool returnNull = false, int? failOnCall = null) : IEmbeddingProvider
    {
        public int CallCount { get; private set; }
        public List<string> TaskTypes { get; } = [];
        public List<string> EmbeddedTexts { get; } = [];

        public Task<IReadOnlyList<float[]>?> EmbedAsync(
            IReadOnlyList<string> texts,
            string taskType,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            TaskTypes.Add(taskType);
            EmbeddedTexts.AddRange(texts);
            if (returnNull || CallCount == failOnCall)
                return Task.FromResult<IReadOnlyList<float[]>?>(null);

            IReadOnlyList<float[]> vectors = texts.Select(_ => new[] { 1f, 0f }).ToArray();
            return Task.FromResult<IReadOnlyList<float[]>?>(vectors);
        }
    }
}
