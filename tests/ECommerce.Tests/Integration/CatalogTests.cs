using System.Net;
using System.Net.Http.Json;
using ECommerce.Application.Common;
using ECommerce.Application.DTOs.Catalog;
using FluentAssertions;

namespace ECommerce.Tests.Integration;

public class CatalogTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public CatalogTests(CustomWebApplicationFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task GetProducts_Returns200WithSeededItems()
    {
        var response = await _client.GetAsync("/api/products");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var paged = await response.Content.ReadFromJsonAsync<PagedResult<ProductDto>>();
        paged!.Items.Should().NotBeEmpty();
        paged.TotalCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetProductById_SeededId_Returns200()
    {
        var listResponse = await _client.GetAsync("/api/products");
        var paged = await listResponse.Content.ReadFromJsonAsync<PagedResult<ProductDto>>();
        var seededId = paged!.Items.First().Id;

        var response = await _client.GetAsync($"/api/products/{seededId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var product = await response.Content.ReadFromJsonAsync<ProductDto>();
        product!.Id.Should().Be(seededId);
    }

    [Fact]
    public async Task GetProductById_NonExistentId_Returns404()
    {
        var response = await _client.GetAsync("/api/products/999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
