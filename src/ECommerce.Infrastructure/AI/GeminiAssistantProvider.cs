using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ECommerce.Application.DTOs.AI;
using ECommerce.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ECommerce.Infrastructure.AI;

public sealed class GeminiAssistantProvider : IEmbeddingProvider, IAssistantAnswerProvider
{
    private const int EmbeddingBatchSize = 32;
    private readonly HttpClient _http;
    private readonly GeminiOptions _options;
    private readonly ILogger<GeminiAssistantProvider> _logger;

    public GeminiAssistantProvider(
        HttpClient http,
        IOptions<GeminiOptions> options,
        ILogger<GeminiAssistantProvider> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
        _http.Timeout = TimeSpan.FromSeconds(Math.Clamp(_options.RequestTimeoutSeconds, 3, 30));
    }

    public async Task<IReadOnlyList<float[]>?> EmbedAsync(
        IReadOnlyList<string> texts,
        string taskType,
        CancellationToken cancellationToken = default)
    {
        if (!_options.EmbeddingEnabled || string.IsNullOrWhiteSpace(_options.ApiKey) || texts.Count == 0)
            return null;

        var embeddings = new List<float[]>(texts.Count);
        for (var offset = 0; offset < texts.Count; offset += EmbeddingBatchSize)
        {
            var batch = texts.Skip(offset).Take(EmbeddingBatchSize).ToArray();
            var requests = batch.Select(text => new
            {
                model = $"models/{_options.EmbeddingModel}",
                content = new { parts = new[] { new { text } } },
                taskType,
                outputDimensionality = Math.Clamp(_options.EmbeddingDimensions, 128, 3072)
            }).ToArray();

            using var request = CreateRequest(
                HttpMethod.Post,
                $"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(_options.EmbeddingModel)}:batchEmbedContents",
                new { requests });

            try
            {
                using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    LogProviderFailure("embedding", response.StatusCode);
                    return null;
                }

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                if (!document.RootElement.TryGetProperty("embeddings", out var values) ||
                    values.ValueKind != JsonValueKind.Array ||
                    values.GetArrayLength() != batch.Length)
                {
                    _logger.LogWarning("Gemini embedding response did not contain the expected vector count.");
                    return null;
                }

                foreach (var embedding in values.EnumerateArray())
                {
                    if (!embedding.TryGetProperty("values", out var vector) || vector.ValueKind != JsonValueKind.Array)
                        return null;

                    var numbers = vector.EnumerateArray().Select(value => value.GetSingle()).ToArray();
                    if (numbers.Length == 0 || numbers.Any(number => !float.IsFinite(number)))
                        return null;

                    embeddings.Add(numbers);
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Gemini embedding request timed out.");
                return null;
            }
            catch (HttpRequestException exception)
            {
                _logger.LogWarning("Gemini embedding request failed: {FailureType}.", exception.GetType().Name);
                return null;
            }
            catch (JsonException)
            {
                _logger.LogWarning("Gemini embedding response was not valid JSON.");
                return null;
            }
        }

        return embeddings;
    }

    public async Task<AssistantGeneratedAnswer?> GenerateAsync(
        string query,
        IReadOnlyList<AssistantRetrievalCandidate> candidates,
        IReadOnlyList<AssistantConversationTurn> recentTurns,
        CancellationToken cancellationToken = default)
    {
        if (!_options.GenerationEnabled || string.IsNullOrWhiteSpace(_options.ApiKey) || candidates.Count == 0)
            return null;

        var productFacts = candidates.Select(candidate => new
        {
            id = candidate.Product.Id,
            name = candidate.Product.Name,
            description = candidate.Product.Description,
            priceUsd = candidate.Product.Price,
            stock = candidate.Product.Stock,
            category = candidate.Product.CategoryName,
            brand = candidate.Product.BrandName,
            attributes = candidate.Product.Attributes?.Select(attribute => new
            {
                attribute.Code,
                attribute.DisplayName,
                attribute.ValueText,
                attribute.ValueNumber,
                attribute.ValueBoolean,
                attribute.Unit
            })
        }).ToArray();

        var conversation = recentTurns
            .TakeLast(6)
            .Select(turn => new { role = turn.Role, content = turn.Content })
            .ToArray();

        var prompt = JsonSerializer.Serialize(new
        {
            userQuery = query,
            recentConversation = conversation,
            backendCatalogEvidence = productFacts,
            outputContract = new
            {
                answer = "A concise Vietnamese answer grounded only in backendCatalogEvidence.",
                sourceProductIds = "Only IDs from backendCatalogEvidence; [] if evidence is insufficient."
            }
        });

        var systemInstruction = """
            You are the product assistant for a marketplace.
            Treat backendCatalogEvidence as the complete and authoritative product evidence for this request.
            Never invent products, prices, stock, brands, categories, specifications, reviews, or product IDs.
            Do not claim a product has a feature unless that feature appears in its supplied attributes or description.
            If the evidence does not answer the user's request, return an empty sourceProductIds array.
            Return JSON only with exactly the fields answer and sourceProductIds.
            """;

        using var request = CreateRequest(
            HttpMethod.Post,
            $"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(_options.GenerationModel)}:generateContent",
            new
            {
                systemInstruction = new { parts = new[] { new { text = systemInstruction } } },
                contents = new[] { new { role = "user", parts = new[] { new { text = prompt } } } },
                generationConfig = new
                {
                    responseMimeType = "application/json",
                    temperature = 0.2,
                    maxOutputTokens = Math.Clamp(_options.MaxOutputTokens, 128, 1200)
                }
            });

        try
        {
            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                LogProviderFailure("generation", response.StatusCode);
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var text = document.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();

            if (string.IsNullOrWhiteSpace(text))
                return null;

            using var answerDocument = JsonDocument.Parse(text);
            var root = answerDocument.RootElement;
            var answer = root.TryGetProperty("answer", out var answerValue)
                ? answerValue.GetString()?.Trim()
                : null;
            if (string.IsNullOrWhiteSpace(answer))
                return null;

            var sourceIds = root.TryGetProperty("sourceProductIds", out var sourceValue) &&
                            sourceValue.ValueKind == JsonValueKind.Array
                ? sourceValue.EnumerateArray()
                    .Where(value => value.TryGetInt32(out _))
                    .Select(value => value.GetInt32())
                    .Where(id => id > 0)
                    .Distinct()
                    .Take(8)
                    .ToArray()
                : [];

            return new AssistantGeneratedAnswer(answer[..Math.Min(answer.Length, 1500)], sourceIds);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Gemini answer generation timed out.");
            return null;
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning("Gemini answer generation failed: {FailureType}.", exception.GetType().Name);
            return null;
        }
        catch (JsonException)
        {
            _logger.LogWarning("Gemini answer generation response was not valid JSON.");
            return null;
        }
        catch (KeyNotFoundException)
        {
            _logger.LogWarning("Gemini answer generation response did not contain a candidate.");
            return null;
        }
        catch (IndexOutOfRangeException)
        {
            _logger.LogWarning("Gemini answer generation response did not contain a candidate.");
            return null;
        }
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string url, object body)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.TryAddWithoutValidation("x-goog-api-key", _options.ApiKey);
        request.Content = JsonContent.Create(body);
        return request;
    }

    private void LogProviderFailure(string operation, HttpStatusCode statusCode) =>
        _logger.LogWarning("Gemini {Operation} returned HTTP {StatusCode}.", operation, (int)statusCode);
}
