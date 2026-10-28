namespace Mcacc.Core.Model;

/// <summary>
/// The account that stands for the provider's own installation: it uses the real home directory
/// (<c>~/.claude</c>, <c>~/.codex</c>) instead of a profile of its own, so a login that already exists stays usable.
/// </summary>
public sealed record ProviderDefaults(string AccountId, string AccountName, string Color);
