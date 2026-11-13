namespace ECommerce.Infrastructure.AI;

public sealed class GeminiOptions
{
    public const string SectionName = "Gemini";
    public string? ApiKey { get; set; }
    public string GenerationModel { get; set; } = "gemini-3.8-flash";
    public string EmbeddingModel { get; set; } = "gemini-embedding-001";
    public int EmbeddingDimensions { get; set; } = 768;
    public string GenerationThinkingLevel { get; set; } = "low";
    public int MaxOutputTokens { get; set; } = 600;
    public int EmbeddingBatchDelaySeconds { get; set; } = 3;
    public int EmbeddingRetryMaxAttempts { get; set; } = 5;
    public int EmbeddingRetryBaseDelaySeconds { get; set; } = 15;
    public int EmbeddingRetryMaxDelaySeconds { get; set; } = 90;
    public int EmbeddingRetryMaxTotalWaitSeconds { get; set; } = 300;
}
