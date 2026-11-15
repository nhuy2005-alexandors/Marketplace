using System.Net;
using System.Text;
using System.Text.Json;
using ECommerce.Application.DTOs.AI;
using ECommerce.Application.DTOs.Catalog;
using ECommerce.Infrastructure.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ECommerce.Tests.Unit;

public sealed class GeminiAssistantProviderTests
{
    [Fact]
    public async Task EmbedAsync_UsesConfiguredTaskTypeAndReturnsVectorsInInputOrder()
    {
        string? capturedUrl = null;
        string? capturedApiKey = null;
        string? capturedBody = null;
        var handler = new StubHandler(async (request, ct) =>
        {
            capturedUrl = request.RequestUri!.AbsoluteUri;
            capturedApiKey = request.Headers.GetValues("x-goog-api-key").Single();
            capturedBody = await request.Content!.ReadAsStringAsync(ct);
            return JsonResponse(
                """{"embeddings":[{"values":[0.1,0.2]},{"values":[0.3,0.4]}]}""");
        });
        using var http = new HttpClient(handler);
        var provider = Provider(http);

        var result = await provider.EmbedAsync(["document one", "document two"], "RETRIEVAL_QUERY");

        Assert.NotNull(result);
        Assert.Equal(2, result!.Count);
        Assert.Equal(0.1f, result[0][0], 5);
        Assert.Equal(0.4f, result[1][1], 5);
        Assert.Contains("batchEmbedContents", capturedUrl);
        Assert.Equal("test-api-key", capturedApiKey);

        using var body = JsonDocument.Parse(capturedBody!);
        var requests = body.RootElement.GetProperty("requests");
        Assert.Equal(2, requests.GetArrayLength());
        Assert.Equal("RETRIEVAL_QUERY", requests[0].GetProperty("taskType").GetString());
        Assert.Equal(768, requests[0].GetProperty("outputDimensionality").GetInt32());
    }

    [Fact]
    public async Task GenerateAsync_ParsesStructuredAnswerAndDistinctSourceIds()
    {
        var responseBody = """
            {
              "candidates": [
                {
                  "content": {
                    "parts": [
                      {
                        "text": "{\"answer\":\"Tai nghe này phù hợp.\",\"sourceProductIds\":[10,10,11,999]}"
                      }
                    ]
                  }
                }
              ]
            }
            """;
        string? capturedUrl = null;
        string? capturedBody = null;
        var handler = new StubHandler(async (request, ct) =>
        {
            capturedUrl = request.RequestUri!.AbsoluteUri;
            capturedBody = await request.Content!.ReadAsStringAsync(ct);
            return JsonResponse(responseBody);
        });
        using var http = new HttpClient(handler);
        var provider = Provider(http);

        var candidate = Candidate(10, "Tai nghe Sony");
        var result = await provider.GenerateAsync(
            "tìm tai nghe",
            [candidate],
            [new AssistantConversationTurn("user", "tìm tai nghe")]);

        Assert.NotNull(result);
        Assert.Equal("Tai nghe này phù hợp.", result!.Answer);
        Assert.Equal([10, 11, 999], result.SourceProductIds);
        Assert.Contains("gemini-3.8-flash:generateContent", capturedUrl);

        using var body = JsonDocument.Parse(capturedBody!);
        Assert.False(body.RootElement.GetProperty("generationConfig").TryGetProperty("temperature", out _));
        Assert.Equal("application/json",
            body.RootElement.GetProperty("generationConfig").GetProperty("responseMimeType").GetString());
    }

    [Fact]
    public async Task EmbedAsync_ReturnsNullOnProviderRateLimitWithoutExposingResponseBody()
    {
        var handler = new StubHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            {
                Content = new StringContent("sensitive provider response")
            }));
        using var http = new HttpClient(handler);
        var provider = Provider(http);

        var result = await provider.EmbedAsync(["query"], "RETRIEVAL_QUERY");

        Assert.Null(result);
    }

    [Fact]
    public async Task GenerateAsync_ReturnsNullWhenProviderReturnsMalformedCandidatePayload()
    {
        var handler = new StubHandler((_, _) => Task.FromResult(JsonResponse("""{"candidates":[]}""")));
        using var http = new HttpClient(handler);
        var provider = Provider(http);

        var result = await provider.GenerateAsync("query", [Candidate(10, "Headphones")], []);

        Assert.Null(result);
    }

    [Fact]
    public async Task DisabledEmbeddingDoesNotSendHttpRequest()
    {
        var handler = new StubHandler((_, _) => throw new InvalidOperationException("Unexpected HTTP request."));
        using var http = new HttpClient(handler);
        var provider = Provider(http, embeddingEnabled: false);

        var result = await provider.EmbedAsync(["query"], "RETRIEVAL_QUERY");

        Assert.Null(result);
    }

    private static GeminiAssistantProvider Provider(HttpClient http, bool embeddingEnabled = true) =>
        new(
            http,
            Options.Create(new GeminiOptions
            {
                ApiKey = "test-api-key",
                EmbeddingEnabled = embeddingEnabled,
                GenerationEnabled = true,
                EmbeddingModel = "gemini-embedding-001",
                GenerationModel = "gemini-3.8-flash",
                EmbeddingDimensions = 768,
                MaxOutputTokens = 600,
                RequestTimeoutSeconds = 12
            }),
            NullLogger<GeminiAssistantProvider>.Instance);

    private static AssistantRetrievalCandidate Candidate(int id, string name)
    {
        var product = new ProductDto(
            id, name, "Catalog description", 89m, 5, null, 1, "Audio",
            2, "Approved Shop", 4.5, 12);
        return new AssistantRetrievalCandidate(product, 0.9, 0.8, 0.9, $"product:{id}", $"hash-{id}");
    }

    private static HttpResponseMessage JsonResponse(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    private sealed class StubHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => send(request, cancellationToken);
    }
}
