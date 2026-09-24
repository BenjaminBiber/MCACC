namespace Mcacc.Core.Stats;

/// <summary>API list prices in USD per million tokens.</summary>
public sealed record ModelPrice(
    decimal Input,
    decimal Output,
    decimal CacheWrite5Min,
    decimal CacheWrite1Hour,
    decimal CacheRead)
{
    /// <summary>
    /// Cache writes cost 1.25 times (5 minutes) and 2 times (1 hour) the input price, a cache read a tenth of it,
    /// unless the model has its own cache read price.
    /// </summary>
    public static ModelPrice Standard(decimal input, decimal output, decimal? cacheRead = null) =>
        new(input, output, input * 1.25m, input * 2m, cacheRead ?? input * 0.1m);
}
