using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Reflection;
using ECommerce.API.Controllers;
using Microsoft.AspNetCore.Mvc;

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
        Assert.Equal("weak", body.RootElement.GetProperty("evidenceStrength").GetString());
        Assert.Equal("weak-evidence", body.RootElement.GetProperty("answerSource").GetString());
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

    [Fact]
    public async Task Assistant_RejectsOversizedQueryConversationAndVisibleProductContext()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();
        using var longQuery = await client.PostAsJsonAsync("/api/assistant/chat", new { query = new string('q', 1001) });
        using var tooManyTurns = await client.PostAsJsonAsync("/api/assistant/chat", new
        {
            query = "lamp",
            conversation = new { recentTurns = Enumerable.Range(0, 5).Select(index => new { role = "user", content = $"turn {index}" }) }
        });
        using var longTurn = await client.PostAsJsonAsync("/api/assistant/chat", new
        {
            query = "lamp",
            conversation = new { recentTurns = new[] { new { role = "user", content = new string('c', 501) } } }
        });
        using var tooManyVisibleIds = await client.PostAsJsonAsync("/api/assistant/chat", new
        {
            query = "lamp",
            context = new { visibleProductIds = Enumerable.Range(1, 21) }
        });

        Assert.Equal(HttpStatusCode.BadRequest, longQuery.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, tooManyTurns.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, longTurn.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, tooManyVisibleIds.StatusCode);
    }

    [Fact]
    public void Assistant_Declares32KiBRequestBodyLimit()
    {
        var chat = typeof(AssistantController).GetMethod(nameof(AssistantController.Chat), BindingFlags.Instance | BindingFlags.Public);
        var limit = chat?.CustomAttributes.SingleOrDefault(attribute => attribute.AttributeType == typeof(RequestSizeLimitAttribute));
        Assert.NotNull(limit);
        Assert.Equal(32L * 1024, Convert.ToInt64(limit.ConstructorArguments.Single().Value));
    }
}
