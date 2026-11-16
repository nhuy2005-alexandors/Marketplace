namespace ECommerce.Infrastructure.AI;

public sealed class GeminiOptions
{
    public const string SectionName = "Gemini";
    public string? ApiKey { get; set; }
    public string? GenerationBaseUrl { get; set; }
    public string? GenerationApiKey { get; set; }
    public bool GenerationEnabled { get; set; } = false;
    public string GenerationModel { get; set; } = "gemini-3.8-flash";
    public string EmbeddingModel { get; set; } = "gemini-embedding-001";
    public int EmbeddingDimensions { get; set; } = 768;
    public string GenerationThinkingLevel { get; set; } = "low";
    public int MaxOutputTokens { get; set; } = 600;
    public int GenerationTimeoutSeconds { get; set; } = 10;
    public int GenerationFailureThreshold { get; set; } = 3;
    public int GenerationCircuitOpenSeconds { get; set; } = 60;
    public int EmbeddingBatchDelaySeconds { get; set; } = 3;
    public int EmbeddingRetryMaxAttempts { get; set; } = 5;
    public int EmbeddingRetryBaseDelaySeconds { get; set; } = 15;
    public int EmbeddingRetryMaxDelaySeconds { get; set; } = 90;
    public int EmbeddingRetryMaxTotalWaitSeconds { get; set; } = 300;
    public bool StrictEvidenceEnabled { get; set; } = true;
    public bool ContextualizerV2Enabled { get; set; } = true;
    public bool EmbeddingWarmupEnabled { get; set; } = true;
    public bool LexicalFallbackWhileWarming { get; set; } = true;
    public string? EmbeddingCachePath { get; set; }
    public double RetrievalSemanticThreshold { get; set; } = 0.38;
    public double RetrievalSemanticWeight { get; set; } = 0.5;
}

public sealed class AssistantDomainOptions
{
    public const string SectionName = "AssistantDomain";
    public string[] SupportedAttributeCodes { get; set; } = [];
    public Dictionary<string, string[]> AttributeAliases { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, Dictionary<string, string[]>> ValueAliases { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string[]> UnitAliases { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string[] FollowUpCues { get; set; } = [];
    public string[] UnknownAttributeAliases { get; set; } = [];
}
