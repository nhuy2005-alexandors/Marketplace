using ECommerce.Infrastructure.Payments;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ECommerce.Tests.Unit;

public class MoMoTransportTests
{
    [Theory]
    [InlineData(403, "<html>403 Forbidden</html>", "MoMo gateway returned HTTP 403. Payment initiation was not confirmed; do not retry this attempt.")]
    [InlineData(502, "{\"resultCode\":0,\"payUrl\":\"https://test-payment.momo.vn/checkout\"}", "MoMo gateway returned HTTP 502. Payment initiation was not confirmed; do not retry this attempt.")]
    [InlineData(200, "<html>Gateway error</html>", "MoMo gateway returned an invalid JSON response. Payment initiation was not confirmed; do not retry this attempt.")]
    [InlineData(200, "{\"resultCode\":0,\"payUrl\":\"https://test-payment.momo.vn/checkout\"}", null)]
    public async Task Initiation_distinguishes_http_failure_invalid_json_and_valid_redirect(
        int status, string responseBody, string? expectedError)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        await using var gateway = builder.Build();
        gateway.MapPost("/create", () => Results.Content(responseBody, "text/html", statusCode: status));
        await gateway.StartAsync();
        var address = gateway.Services.GetRequiredService<IServer>().Features
            .Get<IServerAddressesFeature>()!.Addresses.Single();
        var provider = new MoMoProvider(Options.Create(new PaymentOptions { MoMo = new()
        {
            PartnerCode = "test", AccessKey = "test", SecretKey = "test",
            CatalogCurrency = "USD", CatalogToVndRate = 25637m, Endpoint = address + "/create"
        } }));

        var result = await provider.CreatePaymentAsync(new(42, 1m, "", "", "42-transport-test"));

        Assert.False(result.Completed);
        Assert.Equal(expectedError, result.Error);
        if (expectedError is null)
        {
            Assert.Equal("https://test-payment.momo.vn/checkout", result.RedirectUrl);
            Assert.Equal("42-transport-test", result.TransactionId);
            Assert.Equal(25637m, result.ProviderAmount);
            Assert.Equal("VND", result.ProviderCurrency);
        }
        else
        {
            Assert.Null(result.RedirectUrl);
            Assert.Empty(result.TransactionId);
        }
    }
}
