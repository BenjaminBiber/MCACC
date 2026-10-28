using Mcacc.Core.Model;
using Mcacc.Core.Processes;
using Mcacc.Core.Quoting;

namespace Mcacc.Core.Terminal;

/// <param name="ConfigDirOverride">
/// Null means the provider's config-dir variable must be <em>unset</em> (the account that uses the provider's own home).
/// </param>
public sealed record CliLaunchRequest(
    ProviderDescriptor Provider,
    string AccountId,
    string AccountName,
    string? ConfigDirOverride,
    string HomeDir,
    string LaunchDir,
    string TabColor)
{
    public string Title => TitleSanitizer.Sanitize($"{Provider.CliDisplayName} · {AccountName}");

    /// <summary>The variable the generated script sets, or unsets when <see cref="ConfigDirOverride"/> is null.</summary>
    public string ConfigDirVariable => Provider.Cli.ConfigDirVariable;

    public string CliCommand => Provider.Cli.Command;

    /// <summary>What the generated script removes before starting the CLI.</summary>
    public IReadOnlyList<string> Scrub => EnvironmentScrub.For(Provider);
}
