using Mcacc.Core.ConfigCopy;

namespace Mcacc.Core.Model;

/// <summary>
/// What MCACC knows about a provider without looking at the disk: names, the folders a profile is made of, and how
/// to address its command line and its desktop app. Pure data — no IO, and no text meant for the user interface.
/// </summary>
/// <param name="ProfileSubfolder">Below a profile directory: the provider's own configuration home.</param>
/// <param name="DesktopSubfolder">Below a profile directory: the desktop app's user data.</param>
/// <param name="Desktop">Null for a provider whose desktop app MCACC cannot start per profile.</param>
/// <param name="CopyItems">
/// What may be copied from one of this provider's accounts into a new one. Each item belongs to exactly one
/// provider, because two providers can keep different things under the same name.
/// </param>
public sealed record ProviderDescriptor(
    ProviderKind Kind,
    string DisplayName,
    string CliDisplayName,
    string DesktopDisplayName,
    string ProfileSubfolder,
    string DesktopSubfolder,
    ProviderCli Cli,
    ProviderDesktop? Desktop,
    ProviderDefaults Defaults,
    IReadOnlyList<CopyItem> CopyItems);
