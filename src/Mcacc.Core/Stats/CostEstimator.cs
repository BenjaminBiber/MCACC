namespace Mcacc.Core.Stats;

public static class CostEstimator
{
    /// <returns>USD, or null when the model has no price.</returns>
    public static decimal? Estimate(string model, TokenCounts tokens) =>
        ModelPricing.Find(model) is { } price ? Cost(price, tokens) : null;

    public static decimal Cost(ModelPrice price, TokenCounts tokens) =>
        (tokens.Input * price.Input
         + tokens.Output * price.Output
         + tokens.CacheWrite5Min * price.CacheWrite5Min
         + tokens.CacheWrite1Hour * price.CacheWrite1Hour
         + tokens.CacheRead * price.CacheRead) / 1_000_000m;
}
