using System.Net;
using System.Net.Http.Json;
using ECommerce.Application.DTOs.Auth;
using FluentAssertions;

namespace ECommerce.Tests.Integration;

public class RefreshFlowTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public RefreshFlowTests(CustomWebApplicationFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task Refresh_WithValidToken_ReturnsNewPair_AndRevokesOld()
    {
        var login = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("seller1@shop.com", "Seller@123"));
        var loginBody = await login.Content.ReadFromJsonAsync<AuthResponse>();
        var oldRefreshToken = loginBody!.RefreshToken;

        var refreshResponse = await _client.PostAsJsonAsync("/api/auth/refresh",
            new RefreshRequest(oldRefreshToken));

        refreshResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var newBody = await refreshResponse.Content.ReadFromJsonAsync<AuthResponse>();
        newBody!.RefreshToken.Should().NotBe(oldRefreshToken);

        // Reusing the OLD refresh token again must fail: rotation revokes it.
        var reuseResponse = await _client.PostAsJsonAsync("/api/auth/refresh",
            new RefreshRequest(oldRefreshToken));
        reuseResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_ThenRefresh_Returns401()
    {
        var login = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("seller2@shop.com", "Seller@123"));
        var loginBody = await login.Content.ReadFromJsonAsync<AuthResponse>();

        var logoutResponse = await _client.PostAsJsonAsync("/api/auth/logout",
            new LogoutRequest(loginBody!.RefreshToken));
        logoutResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var refreshAfterLogout = await _client.PostAsJsonAsync("/api/auth/refresh",
            new RefreshRequest(loginBody.RefreshToken));
        refreshAfterLogout.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
