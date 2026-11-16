using ECommerce.Application.DTOs.AI;
using ECommerce.Application.DTOs.Catalog;
using ECommerce.Application.Interfaces;
using ECommerce.Application.Options;
using ECommerce.Application.Services;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using ECommerce.Infrastructure.AI;
using ECommerce.Infrastructure.Persistence;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ECommerce.Tests.Unit;

public sealed class HybridAiIntegrityTests
{
    private static readonly DateTime Cutoff = new(2026, 11, 16, 0, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData("tai nghe dưới 100 USD", 100)]
    [InlineData("tai nghe dưới $59.99", 59.99)]
    [InlineData("headphones up to $100", 100)]
    [InlineData("headphones at most $100", 100)]
    [InlineData("headphones no more than $100", 100)]
    [InlineData("headphones maximum 100 USD", 100)]
    public void Vietnamese_budget_preserves_currency_and_decimal(string query, decimal maximum)
        => Assert.Equal(maximum, AssistantQueryFilters.Merge(query, null).MaxPrice);

    [Theory]
    [InlineData("RAM at least 16 GB and storage at most 512 GB")]
    [InlineData("at least 16 GB RAM and at most 512 GB storage")]
    public void Numeric_constraints_bind_each_field_to_its_own_amount_and_comparator(string query)
    {
        var definitions = new[] { Definition(1, "ram"), Definition(2, "storage") };
        var analysis = AssistantQueryAnalyzer.Analyze(query,
            [], [], [], definitions, [], new AssistantDomainOptions { SupportedAttributeCodes = ["ram", "storage"] });
        Assert.Equal(2, analysis.Constraints.Count);
        Assert.Contains(analysis.Constraints, x => x.AttributeDefinitionId == 1 && x.ValueNumber == 16 && x.Operator == "greaterThanOrEqual");
        Assert.Contains(analysis.Constraints, x => x.AttributeDefinitionId == 2 && x.ValueNumber == 512 && x.Operator == "lessThanOrEqual");
    }

    [Fact]
    public async Task Explicit_filter_can_retrieve_without_a_catalog_word_in_strict_mode()
    {
        var search = new FakeSearch(hasCatalogTerm: false);
        var response = await Assistant(search).ChatAsync(new("tìm sản phẩm", new(CategoryId: 1)), null);
        Assert.True(search.SearchCalled);
        Assert.Single(response.Products);
    }

    [Fact]
    public async Task Generated_prose_cannot_override_catalog_facts()
    {
        var response = await Assistant(new FakeSearch(), new("Giá $1.00, tồn kho 999; bảo hành vĩnh viễn.", [10]))
            .ChatAsync(new("tai nghe"), null);
        Assert.DoesNotContain("$1.00", response.Answer);
        Assert.DoesNotContain("999", response.Answer);
        Assert.DoesNotContain("vĩnh viễn", response.Answer);
        Assert.Contains("1000", response.Answer);
        Assert.Equal("generated", response.AnswerSource);
    }

    [Fact]
    public async Task Mixed_known_and_unknown_citations_discard_the_entire_generated_selection()
    {
        var response = await Assistant(new FakeSearch(), new("Unsupported model text", [10, 999]))
            .ChatAsync(new("tai nghe"), null);
        Assert.Equal("fallback", response.AnswerSource);
        Assert.DoesNotContain("Unsupported model text", response.Answer);
    }

    [Fact]
    public async Task Empty_model_selection_cannot_erase_existing_strong_catalog_evidence()
    {
        var response = await Assistant(new FakeSearch(), new("No products exist", []))
            .ChatAsync(new("tai nghe"), null);
        Assert.Equal(10, Assert.Single(response.Products).Id);
        Assert.True(response.Retrieval.HasEvidence);
        Assert.Equal("fallback", response.AnswerSource);
        Assert.DoesNotContain("No products exist", response.Answer);
    }

    [Fact]
    public async Task Rag_and_recommendation_never_return_an_unapproved_seller()
    {
        using var db = TestDb.Create();
        await Seed(db, SellerStatus.Pending);
        var search = new SemanticSearchService(db, new FakeEmbeddings(), NullLogger<SemanticSearchService>.Instance);
        Assert.Empty(await search.SearchCandidatesAsync("tai nghe", null, 8));
        var recommended = await Recommend(db).GetForUserAsync(null, asOfUtc: Cutoff);
        Assert.Empty(recommended.Items);
    }

    [Theory]
    [InlineData("demo")]
    [InlineData("reconciliation")]
    [InlineData("refund")]
    [InlineData("future-paid")]
    public async Task Recommendation_and_rfm_share_purchase_eligibility(string excluded)
    {
        using var db = TestDb.Create();
        var product = await Seed(db);
        var order = Purchase(product);
        order.Payment!.IsDemo = excluded == "demo";
        order.Payment.RequiresReconciliation = excluded == "reconciliation";
        order.RefundPending = excluded == "refund";
        if (excluded == "future-paid") order.Payment.PaidAt = Cutoff.AddDays(1);
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        var recommendation = await Recommend(db).GetForUserAsync(3, asOfUtc: Cutoff, cutoffExclusiveUtc: Cutoff);
        var rfm = await new RfmService(db).GetCustomersAsync(1, 20, Cutoff);
        Assert.NotEqual("personalized", recommendation.Strategy);
        Assert.Equal(0, Assert.Single(rfm.Items).Frequency);
    }

    [Fact]
    public async Task Paid_purchase_with_pending_fulfillment_is_still_eligible()
    {
        using var db = TestDb.Create();
        var product = await Seed(db);
        db.Orders.Add(Purchase(product));
        await db.SaveChangesAsync();
        Assert.Equal("personalized", (await Recommend(db).GetForUserAsync(3, asOfUtc: Cutoff)).Strategy);
        Assert.Equal(1, Assert.Single((await new RfmService(db).GetCustomersAsync(1, 20, Cutoff)).Items).Frequency);
    }

    [Fact]
    public void Cancelled_only_order_does_not_increase_purchase_profile_frequency()
    {
        var order = Purchase(new Product { Id = 10, CategoryId = 1, Name = "Fixture" });
        order.Items.Single().Status = FulfillmentStatus.Cancelled;
        Assert.Equal(0, RecommendationService.BuildPurchaseProfile([order]).Frequency);
    }

    [Fact]
    public async Task Non_comparable_attributes_cannot_create_personalization()
    {
        using var db = TestDb.Create();
        var product = await Seed(db);
        var definition = Definition(1, "ram");
        definition.IsComparable = false;
        var candidate = new Product { Id = 11, Sku = "SYNTH-11", Name = "Other", CategoryId = 1, SellerId = 2, Price = 1000, Stock = 5, CreatedAt = Cutoff.AddDays(-20) };
        db.AttributeDefinitions.Add(definition);
        db.Products.Add(candidate);
        foreach (var p in new[] { product, candidate })
            db.ProductAttributeValues.Add(new ProductAttributeValue { ProductId = p.Id, Product = p, CategoryId = 1,
                AttributeDefinitionId = 1, AttributeDefinition = definition, DataType = CatalogAttributeType.Number, ValueNumber = 16 });
        db.Orders.Add(Purchase(product));
        await db.SaveChangesAsync();
        var options = new RecommendationOptions { CategoryWeight = 0, BrandWeight = 0, AttributeWeight = 1,
            CharacteristicWeight = 0, PriceWeight = 0, RepeatPurchaseWeight = 0, PersonalizedMinimumScore = .01 };
        Assert.NotEqual("personalized", (await new RecommendationService(db, Options.Create(options)).GetForUserAsync(3, asOfUtc: Cutoff)).Strategy);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("tai nghe dưới 100 USD", true)]
    [InlineData("headphones under $59.99", true)]
    [InlineData("headphones under $59.99", false)]
    public async Task Filtered_recommendation_ranks_eligible_products_before_the_global_top_k(string? previousQuery, bool contextualizerV2)
    {
        using var db = TestDb.Create();
        var first = await Seed(db);
        foreach (var id in Enumerable.Range(11, 24))
            db.Products.Add(new Product { Id = id, Sku = $"SYNTH-{id}", Name = "Tai nghe cao cấp", Price = 1000, Stock = 5,
                CategoryId = 1, SellerId = 2, CreatedAt = Cutoff.AddDays(-20) });
        var cheap = new Product { Id = 99, Sku = "SYNTH-99", Name = "Tai nghe giá thấp", Price = 50, Stock = 5,
            CategoryId = 1, SellerId = 2, CreatedAt = Cutoff.AddDays(-20) };
        db.Products.Add(cheap);
        await db.SaveChangesAsync();
        foreach (var product in db.Products.ToArray())
            db.Reviews.Add(new Review { ProductId = product.Id, UserId = 3, Rating = product.Id == 99 ? 1 : 5,
                Comment = "Synthetic rating", CreatedAt = Cutoff.AddDays(-10) });
        await db.SaveChangesAsync();
        var options = Options.Create(new GeminiOptions { GenerationEnabled = false, StrictEvidenceEnabled = true, ContextualizerV2Enabled = contextualizerV2, EmbeddingBatchDelaySeconds = 0 });
        var search = new SemanticSearchService(db, new FakeEmbeddings(), NullLogger<SemanticSearchService>.Instance, geminiOptions: options);
        var service = new AssistantService(search, new FakeAnswer(null), new RecommendationProviderAdapter(Recommend(db)),
            NullLogger<AssistantService>.Instance, geminiOptions: options);
        var request = previousQuery is not null
            ? new AssistantChatRequest("recommend these", Conversation: new("follow-up-budget", [new("user", previousQuery), new("assistant", "Catalog matches")]))
            : new AssistantChatRequest("gợi ý tai nghe dưới 100 USD", new(MaxPrice: 100));
        var response = await service.ChatAsync(request, null);
        Assert.Equal("recommendation", response.Retrieval.Mode);
        Assert.Equal(99, Assert.Single(response.Products).Id);
    }

    [Fact]
    public async Task Cold_start_without_sales_or_ratings_returns_honest_catalog_candidates()
    {
        using var db = TestDb.Create();
        await Seed(db);
        var response = await Recommend(db).GetForUserAsync(null, asOfUtc: Cutoff);
        Assert.Equal("catalog_fallback", response.Strategy);
        Assert.Single(response.Items);
        Assert.Contains(response.Items[0].Reasons, reason => reason.Code == "catalog_available");
    }

    [Fact]
    public void Warmup_state_from_another_host_cannot_force_this_host_into_lexical_warming()
    {
        using var db = TestDb.Create();
        var first = new SemanticSearchService(db, new FakeEmbeddings(), NullLogger<SemanticSearchService>.Instance);
        var second = new SemanticSearchService(db, new FakeEmbeddings(), NullLogger<SemanticSearchService>.Instance);
        try
        {
            first.MarkWarmupScheduled();
            Assert.Equal("lexical", second.RetrievalMode);
        }
        finally { first.MarkWarmupDisabled(); }
    }

    [Fact]
    public async Task Failed_warmup_uses_lexical_without_retrying_the_exhausted_provider_on_each_chat()
    {
        using var db = TestDb.Create();
        await Seed(db);
        var embeddings = new FakeEmbeddings(fail: true);
        var search = new SemanticSearchService(db, embeddings, NullLogger<SemanticSearchService>.Instance);
        search.MarkWarmupScheduled();
        await search.WarmUpAsync();
        Assert.False(search.IsHybridReady);
        Assert.Equal("lexical", search.RetrievalMode);
        var calls = embeddings.Calls;
        Assert.Single(await search.SearchCandidatesAsync("tai nghe", null, 4));
        Assert.Equal(calls, embeddings.Calls);
    }

    [Fact]
    public async Task Recommendation_preserves_brand_and_all_active_catalog_attributes()
    {
        using var db = TestDb.Create();
        var product = await Seed(db);
        var brand = new Brand { Id = 1, Name = "Fixture Brand", Slug = "fixture-brand", CreatedAt = Cutoff.AddDays(-30) };
        product.BrandId = brand.Id;
        db.Brands.Add(brand);
        var comparable = Definition(1, "ram");
        var displayOnly = Definition(2, "storage");
        displayOnly.IsComparable = false;
        db.AttributeDefinitions.AddRange(comparable, displayOnly);
        foreach (var definition in new[] { comparable, displayOnly })
            db.ProductAttributeValues.Add(new ProductAttributeValue { ProductId = product.Id, CategoryId = 1,
                AttributeDefinitionId = definition.Id, DataType = CatalogAttributeType.Number, ValueNumber = 16 });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var response = await Recommend(db).GetForUserAsync(null, asOfUtc: Cutoff);
        var item = Assert.Single(response.Items).Product;
        Assert.Equal("Fixture Brand", item.BrandName);
        Assert.Equal("SYNTH-10", item.Sku);
        Assert.Equal(new[] { 1, 2 }, item.Attributes!.Select(a => a.AttributeDefinitionId).Order().ToArray());
    }

    private static RecommendationService Recommend(AppDbContext db) => new(db, Options.Create(new RecommendationOptions
        { CategoryWeight = 1, BrandWeight = 0, PriceWeight = 0, AttributeWeight = 0, CharacteristicWeight = 0,
            RepeatPurchaseWeight = 0, PersonalizedMinimumScore = .01 }));

    private static async Task<Product> Seed(AppDbContext db, SellerStatus sellerStatus = SellerStatus.Approved)
    {
        var category = new Category { Id = 1, Name = "Audio", CreatedAt = Cutoff.AddDays(-40) };
        var seller = new User { Id = 2, Email = "seller@example.test", PasswordHash = "fixture", FullName = "Fixture Seller",
            ShopName = "Synthetic", Role = UserRole.Seller, SellerStatus = sellerStatus, CreatedAt = Cutoff.AddDays(-40) };
        db.Categories.Add(category);
        db.Users.AddRange(seller, new User { Id = 3, Email = "customer@example.test", PasswordHash = "fixture", FullName = "Fixture Customer", Role = UserRole.Customer, CreatedAt = Cutoff.AddDays(-40) });
        var product = new Product { Id = 10, Sku = "SYNTH-10", Name = "Tai nghe", Description = "Tai nghe có dây", Price = 1000,
            Stock = 5, CategoryId = 1, Category = category, SellerId = 2, Seller = seller, CreatedAt = Cutoff.AddDays(-20) };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return product;
    }

    private static Order Purchase(Product product)
    {
        var order = new Order { Id = 1, UserId = 3, Status = OrderStatus.Paid, ShippingAddress = "Synthetic", CreatedAt = Cutoff.AddDays(-3) };
        order.Payment = new Payment { Order = order, OrderId = 1, Amount = 1000, Status = PaymentStatus.Completed,
            CreatedAt = Cutoff.AddDays(-3), PaidAt = Cutoff.AddDays(-2) };
        order.Items.Add(new OrderItem { Order = order, OrderId = 1, Product = product, ProductId = product.Id, SellerId = 2,
            ProductName = product.Name, UnitPrice = 1000, Quantity = 1, Status = FulfillmentStatus.Pending, CreatedAt = Cutoff.AddDays(-3) });
        return order;
    }

    private static AttributeDefinition Definition(int id, string code) => new() { Id = id, CategoryId = 1, Code = code,
        DisplayName = code, Unit = "GB", DataType = CatalogAttributeType.Number, IsActive = true, IsComparable = true, IsFilterable = true };

    private static AssistantService Assistant(FakeSearch search, AssistantGeneratedAnswer? answer = null) => new(search,
        new FakeAnswer(answer), new NoRecommendations(), NullLogger<AssistantService>.Instance,
        geminiOptions: Options.Create(new GeminiOptions { GenerationEnabled = answer is not null, StrictEvidenceEnabled = true, ContextualizerV2Enabled = true }));

    private sealed class FakeSearch(bool hasCatalogTerm = true) : ISemanticSearchService
    {
        public bool SearchCalled { get; private set; }
        public Task<IReadOnlyList<int>> FindEligibleProductIdsAsync(AssistantChatFilters? filters, IReadOnlyList<AssistantTypedConstraint> constraints, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<int>>([]);
        public bool IsHybridReady => false;
        public string RetrievalMode => "lexical";
        public Task WarmUpAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task<bool> HasLexicalEvidenceAsync(string query, AssistantChatFilters? filters, CancellationToken ct = default) => Task.FromResult(true);
        public Task<AssistantQueryAnalysis> AnalyzeQueryAsync(string query, CancellationToken ct = default) => Task.FromResult(new AssistantQueryAnalysis([], hasCatalogTerm, false, []));
        public Task<IReadOnlyList<AssistantRetrievalCandidate>> SearchCandidatesAsync(string query, AssistantChatFilters? filters, int limit, CancellationToken ct = default)
            => SearchCandidatesAsync(query, filters, [], limit, ct);
        public Task<IReadOnlyList<AssistantRetrievalCandidate>> SearchCandidatesAsync(string query, AssistantChatFilters? filters,
            IReadOnlyList<AssistantTypedConstraint> constraints, int limit, CancellationToken ct = default)
        {
            SearchCalled = true;
            var product = new ProductDto(10, "Tai nghe", "Có dây", 1000, 5, null, 1, "Audio", 2, "Synthetic", 0, 0);
            return Task.FromResult<IReadOnlyList<AssistantRetrievalCandidate>>([new(product, 1, 0, 1, [])]);
        }
    }

    private sealed class FakeAnswer(AssistantGeneratedAnswer? answer) : IAssistantAnswerProvider
    {
        public Task<AssistantGeneratedAnswer> GenerateAsync(string query, IReadOnlyList<AssistantRetrievalCandidate> candidates,
            IReadOnlyList<AssistantConversationTurn> recentTurns, CancellationToken ct = default) => Task.FromResult(answer!);
    }

    private sealed class NoRecommendations : IRecommendationProvider
    {
        public Task<IReadOnlyList<ProductDto>> GetProductsAsync(int? customerId, int limit, CancellationToken ct = default, IReadOnlyCollection<int>? eligibleProductIds = null) => Task.FromResult<IReadOnlyList<ProductDto>>([]);
        public Task<IReadOnlyList<ProductDto>> GetSimilarProductsAsync(int targetProductId, int limit, CancellationToken ct = default, IReadOnlyCollection<int>? eligibleProductIds = null) => Task.FromResult<IReadOnlyList<ProductDto>>([]);
    }

    private sealed class FakeEmbeddings(bool fail = false) : IEmbeddingProvider
    {
        public int Calls { get; private set; }
        public string ModelVersion => "fixture:2";
        public Task<IReadOnlyList<float[]>> GenerateEmbeddingsAsync(IReadOnlyList<string> texts, bool isQuery, CancellationToken ct = default)
        {
            Calls++;
            if (fail) throw new AssistantProviderException("Synthetic quota failure");
            return Task.FromResult<IReadOnlyList<float[]>>(texts.Select(_ => new float[] { 1, 0 }).ToArray());
        }
    }
}
