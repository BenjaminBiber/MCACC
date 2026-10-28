namespace Mcacc.Core.Model;

/// <summary>What is needed to find, recognise and address a provider's desktop application.</summary>
/// <param name="MacBundleName">The <c>.app</c> folder as Finder shows it; unverified for Codex.</param>
/// <param name="DataFolderName">
/// The folder the app keeps its data in, below the roaming application data of its package.
/// </param>
/// <param name="WindowsPathDiscriminator">
/// The path segment that tells this app's processes from another app's. The process name alone does not: both
/// desktop apps run on Chromium, the separate ChatGPT app is also called <c>ChatGPT.exe</c>, and the Claude Code
/// CLI is also called <c>claude(.exe)</c>.
/// </param>
/// <param name="WindowsProcessName">The executable of the app itself, as the process list shows it.</param>
/// <param name="UserDataDirVariable">
/// Null when <c>--user-data-dir</c> alone moves the profile. The Codex app additionally wants the directory named
/// in the environment — its own second-profile launcher sets both.
/// </param>
public sealed record ProviderDesktop(
    string MsixPackageFamilyName,
    string MacBundleId,
    string MacBundleName,
    string DeepLinkScheme,
    string DataFolderName,
    string WindowsPathDiscriminator,
    string WindowsProcessName,
    string? UserDataDirVariable = null);
