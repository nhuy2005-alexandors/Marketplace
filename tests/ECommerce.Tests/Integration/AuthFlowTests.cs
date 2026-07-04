using System.Net;
using System.Net.Http.Json;
using ECommerce.Application.DTOs.Auth;
using FluentAssertions;

namespace ECommerce.Tests.Integration;

public class AuthFlowTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AuthFlowTests(CustomWebApplicationFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task Register_NewUser_Returns200WithTokens()
    {
        var request = new RegisterRequest($"newuser-{Guid.NewGuid():N}@test.com", "Secret@123", "New User");

        var response = await _client.PostAsJsonAsync("/api/auth/register", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        body.Should().NotBeNull();
        body!.Token.Should().NotBeNullOrEmpty();
        body.RefreshToken.Should().NotBeNullOrEmpty();
        body.User.Email.Should().Be(request.Email.ToLowerInvariant());
    }

    [Fact]
    public async Task Login_SeededUser_Returns200()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("user@shop.com", "User@123"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        body!.User.Email.Should().Be("user@shop.com");
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401WithErrorEnvelope()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("user@shop.com", "WrongPassword"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var json = await response.Content.ReadFromJsonAsync<ErrorEnvelope>();
        json!.Error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Me_WithoutToken_Returns401()
    {
        var response = await _client.GetAsync("/api/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_WithToken_Returns200WithUser()
    {
        var login = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("user@shop.com", "User@123"));
        var loginBody = await login.Content.ReadFromJsonAsync<AuthResponse>();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.Authorization = new("Bearer", loginBody!.Token);
        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var user = await response.Content.ReadFromJsonAsync<UserDto>();
        user!.Email.Should().Be("user@shop.com");
    }

    private record ErrorEnvelope(string Error);
}
