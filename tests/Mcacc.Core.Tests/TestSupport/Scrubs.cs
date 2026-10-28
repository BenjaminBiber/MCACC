using Mcacc.Core.Model;
using Mcacc.Core.Processes;

namespace Mcacc.Core.Tests.TestSupport;

/// <summary>
/// Renders the scrub list the way each shell spells it. The list itself is pinned once, in
/// <c>EnvironmentScrubTests</c>; the script tests use this so they stay about structure and quoting.
/// </summary>
internal static class Scrubs
{
    public static string PowerShell(ProviderDescriptor provider) =>
        string.Join(',', EnvironmentScrub.For(provider).Select(name => "Env:" + name));

    public static string Posix(ProviderDescriptor provider) => string.Join(' ', EnvironmentScrub.For(provider));

    public static string[] CmdLines(ProviderDescriptor provider) =>
        [.. EnvironmentScrub.For(provider).Select(name => $"set \"{name}=\"")];
}
