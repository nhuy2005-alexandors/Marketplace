using System.Net;
using System.Net.Http.Json;
using ECommerce.Application.DTOs.AI;
using ECommerce.Application.DTOs.Admin;
using ECommerce.Application.DTOs.Auth;
using ECommerce.Application.DTOs.Catalog;
using ECommerce.Application.DTOs.Recommendations;
using FluentAssertions;

namespace ECommerce.Tests.Integration;

public sealed class OwnerAiEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public OwnerAiEndpointsTests(CustomWebApplicationFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task AssistantChat_AnonymousRequestUsesGroundedCatalogFallback()
    {
        var productsResponse = await _client.GetAsync("/api/products");
        productsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var catalog = await productsResponse.Content.ReadFromJsonAsync<ECommerce.Application.Common.PagedResult<ProductDto>>();
        var product = catalog!.Items.First(item => item.Stock > 0);

        var response = await _client.PostAsJsonAsync(
            "/api/assistant/chat",
            new AssistantChatRequest(product.Name));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AssistantChatResponse>();
        body.Should().NotBeNull();
        body!.Products.Should().Contain(item => item.Id == product.Id);
        body.SourceProductIds.Should().Contain(product.Id);
        body.RetrievalMode.Should().Be("lexical");
        body.AnswerSource.Should().Be("fallback");
    }

    [Fact]
    public async Task Recommendations_AnonymousEndpointsReturnCatalogBackedResponses()
    {
        var response = await _client.GetAsync("/api/recommendations");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var recommendations = await response.Content.ReadFromJsonAsync<RecommendationResponseDto>();
        recommendations.Should().NotBeNull();
        recommendations!.Items.Should().NotBeEmpty();

        var catalogResponse = await _client.GetAsync("/api/products");
        var catalog = await catalogResponse.Content.ReadFromJsonAsync<ECommerce.Application.Common.PagedResult<ProductDto>>();
        var targetId = catalog!.Items.First().Id;

        var similarResponse = await _client.GetAsync($"/api/recommendations/products/{targetId}/similar");

        similarResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        (await similarResponse.Content.ReadFromJsonAsync<RecommendationResponseDto>()).Should().NotBeNull();
    }

    [Fact]
    public async Task RfmCustomers_RequiresAdminRoleAndReturnsPiiSafeClassification()
    {
        var anonymous = await _client.GetAsync("/api/rfm/customers");
        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var customerToken = await LoginAndGetTokenAsync("user@shop.com", "User@123");
        using var customerRequest = new HttpRequestMessage(HttpMethod.Get, "/api/rfm/customers");
        customerRequest.Headers.Authorization = new("Bearer", customerToken);
        var customerResponse = await _client.SendAsync(customerRequest);
        customerResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var adminToken = await LoginAndGetTokenAsync("admin@shop.com", "Admin@123");
        using var adminRequest = new HttpRequestMessage(HttpMethod.Get, "/api/rfm/customers");
        adminRequest.Headers.Authorization = new("Bearer", adminToken);
        var adminResponse = await _client.SendAsync(adminRequest);

        adminResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await adminResponse.Content.ReadFromJsonAsync<RfmCustomerPageDto>();
        page.Should().NotBeNull();
        page!.TotalCustomers.Should().BeGreaterThan(0);
        page.Items.Should().NotContainNull();
        page.Items.Should().OnlyContain(item =>
            item.UserId > 0 &&
            item.SegmentCode.Length > 0 &&
            item.MonetaryUsd >= 0);
    }

    private async Task<string> LoginAndGetTokenAsync(string email, string password)
    {
        var login = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await login.Content.ReadFromJsonAsync<AuthResponse>();
        return body!.Token;
    }
}
