using System.Net;
using System.Net.Http.Json;
using ECommerce.Application.DTOs.Auth;
using FluentAssertions;

namespace ECommerce.Tests.Integration;

public class AuthorizationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AuthorizationTests(CustomWebApplicationFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task AdminDashboard_WithoutToken_Returns401()
    {
        var response = await _client.GetAsync("/api/admin/dashboard");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AdminDashboard_WithCustomerToken_Returns403()
    {
        var token = await LoginAndGetTokenAsync("user@shop.com", "User@123");

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/admin/dashboard");
        request.Headers.Authorization = new("Bearer", token);
        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AdminDashboard_WithAdminToken_Returns200()
    {
        var token = await LoginAndGetTokenAsync("admin@shop.com", "Admin@123");

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/admin/dashboard");
        request.Headers.Authorization = new("Bearer", token);
        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task<string> LoginAndGetTokenAsync(string email, string password)
    {
        var login = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        var body = await login.Content.ReadFromJsonAsync<AuthResponse>();
        return body!.Token;
    }
}
