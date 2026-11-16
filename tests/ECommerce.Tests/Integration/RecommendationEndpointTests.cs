using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using ECommerce.API.Controllers;
using ECommerce.API.Services;
using ECommerce.Application.DTOs.Admin;
using ECommerce.Application.DTOs.Recommendations;
using ECommerce.Application.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ECommerce.Tests.Integration;

public sealed class RecommendationEndpointTests
{
    [Fact]
    public async Task Rfm_RequiresAuthentication()
    {
        using var server = CreateServer();
        using var client = server.CreateClient();

        var response = await client.GetAsync("/api/rfm/customers");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Rfm_RequiresAdminRole()
    {
        using var server = CreateServer();
        using var client = server.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", "45");
        client.DefaultRequestHeaders.Add("X-Test-Role", "Customer");

        var response = await client.GetAsync("/api/rfm/customers");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Personalized_IgnoresClientUserIdAndUsesAuthenticatedIdentity()
    {
        using var server = CreateServer();
        using var client = server.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", "45");
        client.DefaultRequestHeaders.Add("X-Test-Role", "Customer");

        var response = await client.GetAsync("/api/recommendations?userId=999999");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        server.Services.GetRequiredService<RecordingRecommendationService>().LastUserId.Should().Be(45);
    }

    [Fact]
    public async Task SimilarProducts_ClampsClientLimitToMaximum()
    {
        using var server = CreateServer();
        using var client = server.CreateClient();

        var response = await client.GetAsync("/api/recommendations/products/12/similar?take=999");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        server.Services.GetRequiredService<RecordingRecommendationService>().LastSimilarLimit.Should().Be(20);
    }

    [Fact]
    public async Task Rfm_AdminResponseDoesNotContainCustomerPii()
    {
        using var server = CreateServer();
        using var client = server.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", "1");
        client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");

        var response = await client.GetAsync("/api/rfm/customers?page=1&pageSize=10");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().Contain("userId");
        body.Should().NotContain("Email");
        body.Should().NotContain("FullName");
        body.Should().NotContain("private@example.test");
    }

    private static TestServer CreateServer()
    {
        var builder = new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddRouting();
                services.AddHttpContextAccessor();
                services.AddScoped<ICurrentUser, CurrentUser>();
                services.AddSingleton<RecordingRecommendationService>();
                services.AddSingleton<IRecommendationService>(provider =>
                    provider.GetRequiredService<RecordingRecommendationService>());
                services.AddSingleton<IRfmService, FakeRfmService>();
                services.AddAuthentication("test")
                    .AddScheme<AuthenticationSchemeOptions, HeaderAuthenticationHandler>("test", _ => { });
                services.AddAuthorization();
                services.AddControllers().AddApplicationPart(typeof(RecommendationsController).Assembly);
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseAuthentication();
                app.UseAuthorization();
                app.UseEndpoints(endpoints => endpoints.MapControllers());
            });
        return new TestServer(builder);
    }

    private sealed class HeaderAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!int.TryParse(Request.Headers["X-Test-User"], out var userId))
                return Task.FromResult(AuthenticateResult.NoResult());

            var role = Request.Headers["X-Test-Role"].ToString();
            var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, userId.ToString()) };
            if (!string.IsNullOrWhiteSpace(role))
                claims.Add(new Claim(ClaimTypes.Role, role));
            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }

    private sealed class RecordingRecommendationService : IRecommendationService
    {
        public int? LastUserId { get; private set; }
        public int? LastSimilarLimit { get; private set; }

        public Task<RecommendationResponseDto> GetForUserAsync(
            int? userId,
            int? take = null,
            DateTime? asOfUtc = null,
            DateTime? cutoffExclusiveUtc = null,
            CancellationToken cancellationToken = default, IReadOnlyCollection<int>? eligibleProductIds = null)
        {
            LastUserId = userId;
            return Task.FromResult(new RecommendationResponseDto("test", DateTime.UtcNow, []));
        }

        public Task<RecommendationResponseDto> GetSimilarAsync(
            int targetProductId,
            int? take = null,
            DateTime? asOfUtc = null,
            DateTime? cutoffExclusiveUtc = null,
            CancellationToken cancellationToken = default, IReadOnlyCollection<int>? eligibleProductIds = null)
        {
            LastSimilarLimit = take;
            return Task.FromResult(new RecommendationResponseDto("test", DateTime.UtcNow, []));
        }
    }

    private sealed class FakeRfmService : IRfmService
    {
        public Task<RfmCustomerPageDto> GetCustomersAsync(
            int page,
            int pageSize,
            DateTime asOfUtc,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new RfmCustomerPageDto(
                asOfUtc,
                page,
                pageSize,
                1,
                [new RfmSegmentCountDto("NoPurchase", "New / no eligible purchases", 1)],
                [new RfmCustomerDto(777, null, null, 0, 0m, null, null, null, "NoPurchase", "New / no eligible purchases")]));
    }
}
