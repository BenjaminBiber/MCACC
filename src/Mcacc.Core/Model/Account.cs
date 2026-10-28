namespace Mcacc.Core.Model;

/// <param name="Provider">
/// Trailing with a default, so a config written before providers existed reads every account as Claude.
/// </param>
public sealed record Account(
    string Id,
    string Name,
    string Color,
    bool IsDefault,
    ProviderKind Provider = ProviderKind.Claude)
{
    public const string DefaultId = "standard";
    public const string DefaultName = "Standard";
    public const string DefaultColor = "#D97757";

    public static Account CreateDefault() => CreateDefault(ProviderKind.Claude);

    /// <summary>The account that stands for the provider's own installation; see <see cref="ProviderDefaults"/>.</summary>
    public static Account CreateDefault(ProviderKind provider)
    {
        var defaults = ProviderDescriptors.For(provider).Defaults;
        return new Account(defaults.AccountId, defaults.AccountName, defaults.Color, IsDefault: true, provider);
    }
}
