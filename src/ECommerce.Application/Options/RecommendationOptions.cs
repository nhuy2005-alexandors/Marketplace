namespace ECommerce.Application.Options;

/// <summary>
/// Deterministic recommendation proposal defaults. These values are implementation
/// defaults, not business facts, and must not be tuned against holdout targets.
/// </summary>
public sealed class RecommendationOptions
{
    public double CategoryWeight { get; set; } = 0.60;
    public double PriceWeight { get; set; } = 0.40;
    public double RepeatPurchaseWeight { get; set; } = 0.10;
    public double SimilarCategoryWeight { get; set; } = 0.60;
    public double SimilarPriceWeight { get; set; } = 0.40;
    public double PersonalizedMinimumScore { get; set; } = 0.25;
    public double SimilarMinimumScore { get; set; } = 0.35;
    public decimal MaximumRelativePriceDifference { get; set; } = 0.50m;
    public int MaximumProductsPerCategory { get; set; } = 2;
    public int DefaultResultCount { get; set; } = 10;

    /// <summary>Previously purchased products may be recommended again, with an explicit reason.</summary>
    public bool AllowRePurchase { get; set; } = true;
}
