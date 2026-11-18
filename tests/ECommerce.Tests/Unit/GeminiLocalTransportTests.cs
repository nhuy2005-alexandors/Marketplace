using System.Net;
using System.Text;
using System.Text.Json;
using ECommerce.Application.DTOs.AI;
using ECommerce.Application.DTOs.Catalog;
using ECommerce.Infrastructure.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ECommerce.Tests.Unit;

public sealed class GeminiLocalTransportTests
{
    [Fact]
    public async Task Local_generation_uses_bearer_chat_protocol_while_embedding_stays_native()
    {
        var handler = new Handler(async request =>
        {
            if (request.RequestUri!.Host == "127.0.0.1")
            {
                Assert.Equal("/v1/chat/completions", request.RequestUri.AbsolutePath);
                Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
                Assert.Equal("proxy-fixture", request.Headers.Authorization.Parameter);
                Assert.False(request.Headers.Contains("x-goog-api-key"));
                using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
                Assert.Equal("gemini-3.8-flash", body.RootElement.GetProperty("model").GetString());
                Assert.True(body.RootElement.TryGetProperty("messages", out _));
                return Json("""{"choices":[{"message":{"content":"{\"sourceProductIds\":[10]}"}}]}""");
            }
            Assert.EndsWith(":batchEmbedContents", request.RequestUri.AbsolutePath);
            Assert.Equal("embedding-fixture", request.Headers.GetValues("x-goog-api-key").Single());
            return Json("""{"embeddings":[{"values":[0.1,0.2]}]}""");
        });
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://generativelanguage.googleapis.com/") };
        var provider = new GeminiAssistantProvider(new Factory(client), Options.Create(new GeminiOptions
        {
            ApiKey = "embedding-fixture", GenerationApiKey = "proxy-fixture", GenerationBaseUrl = "http://127.0.0.1:8318/v1",
            GenerationModel = "gemini-3.8-flash", EmbeddingBatchDelaySeconds = 0
        }), NullLogger<GeminiAssistantProvider>.Instance);
        var product = new ProductDto(10, "Synthetic", null, 12, 3, null, 1, "Audio", 2, "Fixture", 0, 0);
        var answer = await provider.GenerateAsync("choose", [new(product, 1, 0, 1, [])], []);
        Assert.Equal([10], answer.SourceProductIds);
        Assert.Single(await provider.GenerateEmbeddingsAsync(["catalog"], false));
    }

    private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    private sealed class Factory(HttpClient client) : IHttpClientFactory { public HttpClient CreateClient(string name) => client; }
    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => send(request);
    }
}
