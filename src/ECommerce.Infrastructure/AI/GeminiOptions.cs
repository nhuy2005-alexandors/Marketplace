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
}
