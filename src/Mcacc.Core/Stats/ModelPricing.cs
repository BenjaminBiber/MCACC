using System.Text.RegularExpressions;

namespace Mcacc.Core.Stats;

/// <summary>
/// The built-in price table. It is only an estimate of what the tokens would cost on the API: a subscription pays
/// none of it, and the long-context surcharge of some models is left out.
/// </summary>
public static partial class ModelPricing
{
    private static readonly Dictionary<string, ModelPrice> Prices = new(StringComparer.Ordinal)
    {
        ["claude-fable-5-1"] = ModelPrice.Standard(10m, 50m, cacheRead: 0.25m),

        // No published price of its own; taken to be the same as Fable 5.1.
        ["claude-mythos-5-1"] = ModelPrice.Standard(10m, 50m, cacheRead: 0.25m),
        ["claude-fable-5"] = ModelPrice.Standard(10m, 50m, cacheRead: 1m),
        ["claude-mythos-5"] = ModelPrice.Standard(10m, 50m, cacheRead: 1m),
        ["claude-opus-5-5"] = ModelPrice.Standard(4m, 20m, cacheRead: 0.20m),
        ["claude-opus-5"] = ModelPrice.Standard(5m, 25m),
        ["claude-opus-4-8"] = ModelPrice.Standard(5m, 25m),
        ["claude-opus-4-7"] = ModelPrice.Standard(5m, 25m),
        ["claude-opus-4-6"] = ModelPrice.Standard(5m, 25m),
        ["claude-opus-4-5"] = ModelPrice.Standard(5m, 25m),
        ["claude-sonnet-5"] = ModelPrice.Standard(2m, 10m),
        ["claude-sonnet-4-6"] = ModelPrice.Standard(3m, 15m),
        ["claude-sonnet-4-5"] = ModelPrice.Standard(3m, 15m),
        ["claude-sonnet-4"] = ModelPrice.Standard(3m, 15m),
        ["claude-3-7-sonnet"] = ModelPrice.Standard(3m, 15m),
        ["claude-3-5-sonnet"] = ModelPrice.Standard(3m, 15m),
        ["claude-haiku-4-5"] = ModelPrice.Standard(1m, 5m),

        // The retired models below are listed from memory, as the current price list no longer carries them.
        ["claude-opus-4-1"] = ModelPrice.Standard(15m, 75m),
        ["claude-opus-4"] = ModelPrice.Standard(15m, 75m),
        ["claude-3-opus"] = ModelPrice.Standard(15m, 75m),
        ["claude-3-5-haiku"] = ModelPrice.Standard(0.80m, 4m),
        ["claude-3-haiku"] = ModelPrice.Standard(0.25m, 1.25m),
    };

    /// <returns>Null for a model the table does not know; an unknown price is no price, not zero.</returns>
    public static ModelPrice? Find(string model) => Prices.GetValueOrDefault(Normalize(model));

    /// <summary>"claude-opus-4-5-20251101[1m]" becomes "claude-opus-4-5": neither the date nor the context changes the price.</summary>
    public static string Normalize(string model)
    {
        var name = model.Trim().ToLowerInvariant();
        var bracket = name.IndexOf('[', StringComparison.Ordinal);
        if (bracket >= 0)
        {
            name = name[..bracket];
        }

        return DateSuffix().Replace(name, "");
    }

    [GeneratedRegex(@"-\d{8}$")]
    private static partial Regex DateSuffix();
}
