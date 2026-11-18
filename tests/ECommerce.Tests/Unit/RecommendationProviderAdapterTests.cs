using ECommerce.Application.DTOs.Catalog;
using ECommerce.Application.DTOs.Recommendations;
using ECommerce.Application.Interfaces;
using ECommerce.Application;
using ECommerce.Application.Services;
using ECommerce.Infrastructure.AI;
using ECommerce.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.Tests.Unit;

public sealed class RecommendationProviderAdapterTests
{
    [Fact]
    public async Task Adapter_DelegatesPersonalizedRecommendationsToSharedServiceAndPreservesCustomerId()
    {
        var service = new FakeRecommendationService();
        var adapter = new RecommendationProviderAdapter(service);

        var products = await adapter.GetProductsAsync(42, 8);

        Assert.Equal(42, service.LastUserId);
        Assert.Equal(8, service.LastTake);
        Assert.Equal([7], products.Select(product => product.Id));
    }

    [Fact]
    public async Task Adapter_DelegatesSimilarProductsAndClampsLimit()
    {
        var service = new FakeRecommendationService();
        var adapter = new RecommendationProviderAdapter(service);

        var products = await adapter.GetSimilarProductsAsync(17, 200);

        Assert.Equal(17, service.LastTargetProductId);
        Assert.Equal(20, service.LastTake);
        Assert.Equal([8], products.Select(product => product.Id));
    }

    [Fact]
    public void Infrastructure_RegistersRagAdapterAndRecommendationApiOverSameScopedApplicationService()
    {
        var services = new ServiceCollection();
        services.AddApplication();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["ConnectionStrings:DefaultConnection"] = "Server=(localdb)\\MSSQLLocalDB;Database=Test" }).Build();
        services.AddInfrastructure(configuration);

        var recommendationService = Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IRecommendationService));
        var ragProvider = Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IRecommendationProvider));
        Assert.Equal(ServiceLifetime.Scoped, recommendationService.Lifetime);
        Assert.Equal(typeof(RecommendationService), recommendationService.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, ragProvider.Lifetime);
        Assert.Equal(typeof(RecommendationProviderAdapter), ragProvider.ImplementationType);
        Assert.DoesNotContain(services, descriptor => descriptor.ImplementationType?.Name.Contains("NullRecommendation", StringComparison.Ordinal) == true);
    }

    private sealed class FakeRecommendationService : IRecommendationService
    {
        public int? LastUserId { get; private set; }
        public int? LastTake { get; private set; }
        public int? LastTargetProductId { get; private set; }

        public Task<RecommendationResponseDto> GetForUserAsync(int? userId, int? take = null, DateTime? asOfUtc = null,
            DateTime? cutoffExclusiveUtc = null, CancellationToken cancellationToken = default, IReadOnlyCollection<int>? eligibleProductIds = null)
        {
            LastUserId = userId;
            LastTake = take;
            return Task.FromResult(Response(7));
        }

        public Task<RecommendationResponseDto> GetSimilarAsync(int targetProductId, int? take = null, DateTime? asOfUtc = null,
            DateTime? cutoffExclusiveUtc = null, CancellationToken cancellationToken = default, IReadOnlyCollection<int>? eligibleProductIds = null)
        {
            LastTargetProductId = targetProductId;
            LastTake = take;
            return Task.FromResult(Response(8));
        }

        private static RecommendationResponseDto Response(int id)
        {
            var product = new ProductDto(id, $"Product {id}", null, 10m, 1, null, 2, "Category", 3, "Shop", 0, 0);
            return new RecommendationResponseDto("test", DateTime.UtcNow,
                [new RecommendedProductDto(product, 0.9, [])]);
        }
    }
}
