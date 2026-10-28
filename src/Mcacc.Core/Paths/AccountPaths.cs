using Mcacc.Core.Model;

namespace Mcacc.Core.Paths;

/// <param name="Provider">The provider the other paths were derived from.</param>
/// <param name="DesktopUserDataDir">
/// Null for the account that uses the provider's own installation: its desktop app keeps its data where it always does.
/// </param>
/// <param name="ConfigDirOverride">
/// Null means the provider's config-dir variable must be <em>unset</em> for this account, not set to anything.
/// </param>
/// <param name="EffectiveConfigDir">Where the provider's configuration actually lives, overridden or not.</param>
/// <param name="IdentityFile">The file naming the logged-in account.</param>
/// <param name="CredentialsFile">
/// The file holding the token. For Codex this is the same file as <paramref name="IdentityFile"/>.
/// </param>
public sealed record AccountPaths(
    ProviderDescriptor Provider,
    string? DesktopUserDataDir,
    string? ConfigDirOverride,
    string EffectiveConfigDir,
    string IdentityFile,
    string CredentialsFile,
    IReadOnlyList<string> DesktopDataDirs);
