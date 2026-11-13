using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ECommerce.Tests.Integration;

public sealed class AssistantEndpointTests
{
    [Fact]
    public async Task Assistant_DoesNotTreatClientProductIdsAsRetrievalAuthority()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/assistant/chat", new
        {
            query = "unmatched catalog phrase qzxv",
            productIds = new[] { 1 },
            products = new[] { new { id = 1, name = "Injected product", price = 0.01 } }
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Empty(body.RootElement.GetProperty("products").EnumerateArray());
        Assert.Empty(body.RootElement.GetProperty("sources").EnumerateArray());
    }

    [Fact]
    public async Task Assistant_AppliesDedicatedRateLimit()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();
        HttpStatusCode finalStatus = HttpStatusCode.OK;
        for (var index = 0; index < 13; index++)
        {
            using var response = await client.PostAsJsonAsync("/api/assistant/chat", new { query = "" });
            finalStatus = response.StatusCode;
        }
        Assert.Equal(HttpStatusCode.TooManyRequests, finalStatus);
    }
}
