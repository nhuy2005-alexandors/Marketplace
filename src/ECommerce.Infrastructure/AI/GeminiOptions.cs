namespace ECommerce.Infrastructure.AI;

public sealed class GeminiOptions
{
    public const string SectionName = "Gemini";

    public string ApiKey { get; set; } = string.Empty;
    public bool EmbeddingEnabled { get; set; }
    public bool GenerationEnabled { get; set; }
    public string EmbeddingModel { get; set; } = "gemini-embedding-001";
    public string GenerationModel { get; set; } = "gemini-3.8-flash";
    public int EmbeddingDimensions { get; set; } = 768;
    public int MaxOutputTokens { get; set; } = 600;
    public int RequestTimeoutSeconds { get; set; } = 12;
}
