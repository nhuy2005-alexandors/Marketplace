using ECommerce.Application.DTOs.AI;
using ECommerce.Application.DTOs.Catalog;
using ECommerce.Application.Interfaces;
using ECommerce.Domain.Entities;
using ECommerce.Infrastructure.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http;

namespace ECommerce.Tests.Unit;

public sealed class AiRagRemediationTests
{
    [Fact]
    public async Task SemanticSearch_FindsParaphraseAndKeepsSourceMetadata()
    {
        await using var db = TestDb.Create();
        var audio = new Category { Id = 1, Name = "Audio" };
        var accessories = new Category { Id = 2, Name = "Accessories" };
        var seller = new User { Id = 10, Email = "seller@example.test", PasswordHash = "test-hash", FullName = "Seller", ShopName = "Demo shop" };
        db.Categories.AddRange(audio, accessories);
        db.Users.Add(seller);
        db.Products.AddRange(Product(1, "Wireless headphones", audio, 25m, 4, seller), Product(2, "Wireless mouse", accessories, 18m, 0, seller));
        await db.SaveChangesAsync();
        var service = new SemanticSearchService(db, new FakeEmbeddingProvider(text =>
            text.Contains("travel sound", StringComparison.OrdinalIgnoreCase) || text.Contains("headphones", StringComparison.OrdinalIgnoreCase)
                ? [1f, 0f]
                : [0f, 1f]), NullLogger<SemanticSearchService>.Instance);

        var results = await service.SearchCandidatesAsync("travel sound device", null, 4);

        Assert.Equal(1, results[0].Product.Id);
        Assert.Equal("summary", results[0].Sources[0].ChunkType);
        Assert.NotEmpty(results[0].Sources[0].ContentHash);
        Assert.Equal("fake-embedding-v1", results[0].Sources[0].EmbeddingModelVersion);
    }

    [Fact]
    public async Task SemanticSearch_AppliesAllHardFiltersBeforeRanking()
    {
        await using var db = TestDb.Create();
        var audio = new Category { Id = 1, Name = "Audio" };
        var accessories = new Category { Id = 2, Name = "Accessories" };
        var seller10 = new User { Id = 10, Email = "seller@example.test", PasswordHash = "test-hash", FullName = "Seller", ShopName = "Demo shop" };
        var seller11 = new User { Id = 11, Email = "seller2@example.test", PasswordHash = "test-hash", FullName = "Seller 2", ShopName = "Demo shop 2" };
        db.Categories.AddRange(audio, accessories);
        db.Users.AddRange(seller10, seller11);
        db.Products.AddRange(
            Product(1, "Wireless headphones", audio, 25m, 4, seller10),
            Product(2, "Wireless headphones premium", audio, 80m, 5, seller10),
            Product(3, "Wireless headphones", audio, 20m, 5, seller11),
            Product(4, "Wireless headphones", audio, 20m, 0, seller10),
            Product(5, "Wireless headphones", accessories, 20m, 5, seller10));
        await db.SaveChangesAsync();
        var service = new SemanticSearchService(db, new FakeEmbeddingProvider(_ => [1f, 0f]), NullLogger<SemanticSearchService>.Instance);

        var results = await service.SearchCandidatesAsync("wireless headphones", new AssistantChatFilters(
            CategoryId: 1, SellerId: 10, MinPrice: 20, MaxPrice: 30, InStockOnly: true), 8);

        Assert.Single(results);
        Assert.Equal(1, results[0].Product.Id);
        Assert.InRange(results[0].Product.Price, 20, 30);
        Assert.True(results[0].Product.Stock > 0);
    }

    [Fact]
    public async Task SemanticSearch_ReusesDocumentVectorsByContentHash()
    {
        await using var db = TestDb.Create();
        var category = new Category { Id = 1, Name = "Cache test category" };
        var seller = new User { Id = 10, Email = "cache@example.test", PasswordHash = "test-hash", FullName = "Seller" };
        db.Categories.Add(category);
        db.Users.Add(seller);
        var suffix = Guid.NewGuid().ToString("N");
        var product = Product(1, $"Cache test {suffix}", category, 10m, 1, seller);
        product.Description = $"Unique cache content {suffix}";
        db.Products.Add(product);
        await db.SaveChangesAsync();

        var provider = new FakeEmbeddingProvider(_ => [1f, 0f]);
        var service = new SemanticSearchService(db, provider, NullLogger<SemanticSearchService>.Instance);
        await service.SearchCandidatesAsync("cache query", null, 4);
        var firstDocumentCalls = provider.DocumentCallCount;
        await service.SearchCandidatesAsync("different cache query", null, 4);

        Assert.Equal(1, firstDocumentCalls);
        Assert.Equal(firstDocumentCalls, provider.DocumentCallCount);
        Assert.Equal(2, provider.QueryCallCount);
    }

    [Fact]
    public async Task SemanticSearch_FallsBackToLexicalWhenEmbeddingIsUnavailable()
    {
        await using var db = TestDb.Create();
        var category = new Category { Id = 1, Name = "Fallback category" };
        var seller = new User { Id = 10, Email = "fallback@example.test", PasswordHash = "test-hash", FullName = "Seller" };
        db.Categories.Add(category);
        db.Users.Add(seller);
        db.Products.Add(Product(1, "Distinctive lexical fallback lamp", category, 10m, 1, seller));
        await db.SaveChangesAsync();
        var service = new SemanticSearchService(db, new UnavailableEmbeddingProvider(), NullLogger<SemanticSearchService>.Instance);

        var results = await service.SearchCandidatesAsync("distinctive lexical fallback lamp", null, 4);

        Assert.Equal(1, Assert.Single(results).Product.Id);
        Assert.True(results[0].LexicalScore > 0);
        Assert.Equal(0, results[0].SemanticScore);
    }

    [Fact]
    public async Task SemanticSearch_UsesExactSkuSqlFilterIncludingNoResult()
    {
        await using var db = TestDb.Create();
        var category = new Category { Id = 1, Name = "SKU category" };
        var seller = new User { Id = 10, Email = "sku@example.test", PasswordHash = "test-hash", FullName = "Seller" };
        db.Categories.Add(category);
        db.Users.Add(seller);
        var product = Product(1, "SKU indexed product", category, 10m, 2, seller);
        product.Sku = "DEMO-V2-00038";
        db.Products.Add(product);
        await db.SaveChangesAsync();
        var service = new SemanticSearchService(db, new UnavailableEmbeddingProvider(), NullLogger<SemanticSearchService>.Instance);

        var found = await service.SearchCandidatesAsync("Find SKU DEMO-V2-00038", null, 4);
        var absent = await service.SearchCandidatesAsync("Find SKU NONEXISTENT-V2-001", null, 4);

        Assert.Equal(1, Assert.Single(found).Product.Id);
        Assert.Empty(absent);
    }

    [Fact]
    public async Task Assistant_ReturnsNoEvidenceWhenRetrievalIsEmpty()
    {
        var service = new AssistantService(new FakeSearch([]), new FakeAnswerProvider(null), new FakeRecommendationProvider(),
            NullLogger<AssistantService>.Instance);

        var response = await service.ChatAsync(new AssistantChatRequest("unknown product"), null);

        Assert.False(response.Retrieval.HasEvidence);
        Assert.Empty(response.Products);
        Assert.Empty(response.Sources);
    }

    [Fact]
    public async Task Assistant_RespectsProviderNoEvidenceForRetrievedCandidates()
    {
        var service = new AssistantService(new FakeSearch([Candidate(ProductDto(7, "Possible but unsupported item"))]),
            new FakeAnswerProvider(new AssistantGeneratedAnswer("Insufficient evidence", [])), new FakeRecommendationProvider(),
            NullLogger<AssistantService>.Instance);

        var response = await service.ChatAsync(new AssistantChatRequest("question"), null);

        Assert.False(response.Retrieval.HasEvidence);
        Assert.Empty(response.Products);
        Assert.Empty(response.Sources);
    }

    [Fact]
    public async Task Assistant_DoesNotTrustGeneratedSourceIdsAndUsesBackendProductDtos()
    {
        var candidate = Candidate(ProductDto(7, "Real catalog item"));
        var service = new AssistantService(new FakeSearch([candidate]), new FakeAnswerProvider(
            new AssistantGeneratedAnswer("Unsupported answer", [999])), new FakeRecommendationProvider(),
            NullLogger<AssistantService>.Instance);

        var response = await service.ChatAsync(new AssistantChatRequest("find item", Context: new AssistantClientHints([999])), null);

        Assert.Equal("Real catalog item", Assert.Single(response.Products).Name);
        Assert.Contains(response.Sources, source => source.ProductId == 7);
        Assert.DoesNotContain(response.Sources, source => source.ProductId == 999);
        Assert.DoesNotContain("Unsupported answer", response.Answer);
    }

    [Fact]
    public async Task Assistant_UsesSafeGroundedFallbackWhenProviderFails()
    {
        var service = new AssistantService(new FakeSearch([Candidate(ProductDto(9, "Catalog lamp"))]),
            new FakeAnswerProvider(null, fail: true), new FakeRecommendationProvider(), NullLogger<AssistantService>.Instance);

        var response = await service.ChatAsync(new AssistantChatRequest("lamp"), null);

        Assert.Contains("Catalog lamp", response.Answer);
        Assert.Equal(9, Assert.Single(response.Products).Id);
        Assert.True(response.Retrieval.HasEvidence);
    }

    [Fact]
    public void QueryFilters_ParseOnlyExplicitCurrencyAndIntersectWithApiFilters()
    {
        var filters = AssistantQueryFilters.Merge("headphones under $30", new AssistantChatFilters(MinPrice: 10, MaxPrice: 40));
        Assert.Equal(10, filters.MinPrice);
        Assert.Equal(30, filters.MaxPrice);

        var range = AssistantQueryFilters.Merge("from $15 to $25", null);
        Assert.Equal(15, range.MinPrice);
        Assert.Equal(25, range.MaxPrice);

        var ambiguous = AssistantQueryFilters.Merge("under 30", null);
        Assert.Null(ambiguous.MaxPrice);
    }

    [Fact]
    public async Task GeminiProvider_RedactsPiiAndKeepsKeyOutOfRequestBody()
    {
        string? capturedBody = null;
        string? capturedKey = null;
        var handler = new FakeHttpHandler(async (request, _) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync();
            capturedKey = request.Headers.GetValues("x-goog-api-key").Single();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"{\\\"answer\\\":\\\"Hello\\\",\\\"sourceProductIds\\\":[7]}\"}]}}]}")
            };
        });
        var provider = Provider(handler);
        var candidate = Candidate(ProductDto(7, "Demo product"));

        var answer = await provider.GenerateAsync("Contact me test.person@example.com 09012345678 api_key=private-key", [candidate], []);

        Assert.Equal([7], answer.SourceProductIds);
        Assert.Equal("server-key-test", capturedKey);
        Assert.NotNull(capturedBody);
        Assert.DoesNotContain("test.person@example.com", capturedBody);
        Assert.DoesNotContain("09012345678", capturedBody);
        Assert.DoesNotContain("private-key", capturedBody);
        Assert.DoesNotContain("server-key-test", capturedBody);
        using var capturedJson = System.Text.Json.JsonDocument.Parse(capturedBody);
        Assert.Equal("low", capturedJson.RootElement.GetProperty("generationConfig")
            .GetProperty("thinkingConfig").GetProperty("thinkingLevel").GetString());
    }

    [Fact]
    public async Task GeminiProvider_Maps429ToSafeProviderError()
    {
        var provider = Provider(new FakeHttpHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests)
        {
            Content = new StringContent("private provider body")
        })));

        var exception = await Assert.ThrowsAsync<AssistantProviderException>(() =>
            provider.GenerateEmbeddingsAsync(["fictional catalog item"], isQuery: false));

        Assert.DoesNotContain("private provider body", exception.Message);
        Assert.DoesNotContain("server-key-test", exception.Message);
    }

    [Fact]
    public async Task GeminiProvider_Retries429AndHonorsRetryAfter()
    {
        var calls = 0;
        var handler = new FakeHttpHandler((_, _) =>
        {
            if (Interlocked.Increment(ref calls) == 1)
            {
                var limited = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                limited.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromMilliseconds(80));
                return Task.FromResult(limited);
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"embeddings\":[{\"values\":[0.25,0.75]}]}")
            });
        });
        var provider = Provider(handler);
        var timer = System.Diagnostics.Stopwatch.StartNew();

        var vectors = await provider.GenerateEmbeddingsAsync(["synthetic catalog item"], isQuery: false);

        Assert.Equal(2, calls);
        Assert.Equal(2, vectors[0].Length);
        Assert.True(timer.Elapsed >= TimeSpan.FromMilliseconds(70));
    }

    [Fact]
    public async Task GeminiProvider_Retries429WithExponentialBackoffWhenRetryAfterMissing()
    {
        var calls = 0;
        var handler = new FakeHttpHandler((_, _) =>
        {
            if (Interlocked.Increment(ref calls) == 1)
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"embeddings\":[{\"values\":[0.5,0.5]}]}")
            });
        });
        var provider = Provider(handler);
        var timer = System.Diagnostics.Stopwatch.StartNew();

        await provider.GenerateEmbeddingsAsync(["another synthetic catalog item"], isQuery: false);

        Assert.Equal(2, calls);
        Assert.True(timer.Elapsed >= TimeSpan.FromMilliseconds(450));
    }

    [Fact]
    public async Task GeminiProvider_StopsAfterBounded429Attempts()
    {
        var calls = 0;
        var provider = Provider(new FakeHttpHandler((_, _) =>
        {
            Interlocked.Increment(ref calls);
            var limited = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            limited.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.Zero);
            return Task.FromResult(limited);
        }));

        await Assert.ThrowsAsync<AssistantProviderException>(() => provider.GenerateEmbeddingsAsync(["synthetic item"], isQuery: false));

        Assert.Equal(3, calls);
    }

    [Fact]
    public async Task LocalEmbeddingCache_HitsByHashModelAndDimensionsAndMissesWhenHashChanges()
    {
        var path = Path.Combine(Path.GetTempPath(), $"rag-cache-{Guid.NewGuid():N}.json");
        try
        {
            var store = new LocalEmbeddingCacheStore(path);
            const string hash = "CONTENT-HASH-A";
            const string model = "gemini-embedding-001";
            var entry = new EmbeddingCacheEntry(LocalEmbeddingCacheStore.CreateKey(hash, model, 2),
                hash, model, 2, 17, "summary", [0.25f, 0.75f]);
            await store.WriteAsync([entry]);

            var loaded = await store.ReadAsync();
            Assert.Single(loaded);
            Assert.Equal(entry.Vector, loaded[0].Vector);
            Assert.DoesNotContain(loaded, item => item.CacheKey == LocalEmbeddingCacheStore.CreateKey("CONTENT-HASH-B", model, 2));
            Assert.DoesNotContain(loaded, item => item.CacheKey == LocalEmbeddingCacheStore.CreateKey(hash, model, 3));
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public async Task LocalEmbeddingCache_IgnoresCorruptFileAndCanReplaceIt()
    {
        var path = Path.Combine(Path.GetTempPath(), $"rag-corrupt-cache-{Guid.NewGuid():N}.json");
        try
        {
            await File.WriteAllTextAsync(path, "not-json");
            var store = new LocalEmbeddingCacheStore(path);

            Assert.Empty(await store.ReadAsync());
            var entry = new EmbeddingCacheEntry(LocalEmbeddingCacheStore.CreateKey("hash", "model", 2),
                "hash", "model", 2, 1, "description", [1f, 0f]);
            await store.WriteAsync([entry]);

            Assert.Single(await store.ReadAsync());
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    private static Product Product(int id, string name, Category category, decimal price, int stock, User seller) => new()
    {
        Id = id,
        Name = name,
        Description = "Comfortable audio for travel",
        Price = price,
        Stock = stock,
        CategoryId = category.Id,
        Category = category,
        SellerId = seller.Id,
        Seller = seller,
        Reviews = []
    };

    private static ProductDto ProductDto(int id, string name) => new(id, name, "Description", 10m, 3, null, 1, "Demo", 2, "Shop", 0, 0);

    private static AssistantRetrievalCandidate Candidate(ProductDto product) => new(product, .8, .2, .5,
        [new AssistantSourceChunk(product.Id, "Demo", null, "summary", product.Name, "HASH", "fake-v1")]);

    private static GeminiAssistantProvider Provider(FakeHttpHandler handler) => new(
        new FakeHttpClientFactory(new HttpClient(handler) { BaseAddress = new Uri("https://generativelanguage.googleapis.com/") }),
        Options.Create(new GeminiOptions { ApiKey = "server-key-test" }), NullLogger<GeminiAssistantProvider>.Instance);

    private sealed class FakeHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class FakeHttpHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken);
    }

    private sealed class FakeEmbeddingProvider(Func<string, float[]> vector) : IEmbeddingProvider
    {
        private int _documentCallCount;
        private int _queryCallCount;
        public string ModelVersion => "fake-embedding-v1";
        public int DocumentCallCount => Volatile.Read(ref _documentCallCount);
        public int QueryCallCount => Volatile.Read(ref _queryCallCount);
        public Task<IReadOnlyList<float[]>> GenerateEmbeddingsAsync(IReadOnlyList<string> texts, bool isQuery, CancellationToken ct = default)
        {
            if (isQuery) Interlocked.Increment(ref _queryCallCount);
            else Interlocked.Increment(ref _documentCallCount);
            return Task.FromResult<IReadOnlyList<float[]>>(texts.Select(vector).ToArray());
        }
    }

    private sealed class UnavailableEmbeddingProvider : IEmbeddingProvider
    {
        public string ModelVersion => "offline-unavailable-v1";
        public Task<IReadOnlyList<float[]>> GenerateEmbeddingsAsync(IReadOnlyList<string> texts, bool isQuery, CancellationToken ct = default) =>
            throw new AssistantProviderException("simulated unavailable provider");
    }

    private sealed class FakeSearch(IReadOnlyList<AssistantRetrievalCandidate> candidates) : ISemanticSearchService
    {
        public Task<IReadOnlyList<AssistantRetrievalCandidate>> SearchCandidatesAsync(string query, AssistantChatFilters? filters, int limit, CancellationToken ct = default) =>
            Task.FromResult(candidates);
    }

    private sealed class FakeAnswerProvider(AssistantGeneratedAnswer? answer, bool fail = false) : IAssistantAnswerProvider
    {
        public Task<AssistantGeneratedAnswer> GenerateAsync(string query, IReadOnlyList<AssistantRetrievalCandidate> candidates,
            IReadOnlyList<AssistantConversationTurn> recentTurns, CancellationToken ct = default) =>
            fail ? throw new HttpRequestException("simulated provider failure") : Task.FromResult(answer!);
    }

    private sealed class FakeRecommendationProvider : IRecommendationProvider
    {
        public Task<IReadOnlyList<ProductDto>> GetProductsAsync(int? customerId, int limit, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ProductDto>>([]);
    }
}
