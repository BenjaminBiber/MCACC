using System.Collections.ObjectModel;
using System.Runtime.Versioning;
using Mcacc.Core.Abstractions;
using Mcacc.Core.ClaudeCode;
using Mcacc.Core.Model;
using Mcacc.Core.Paths;
using Mcacc.Core.Processes;

namespace Mcacc.Platform.MacOS;

/// <summary>
/// Reads the Claude Code token out of the login keychain. The first read shows a macOS permission prompt. The token
/// only ever travels from stdout into the parser and is never logged.
/// </summary>
[SupportedOSPlatform("macos")]
public sealed class MacKeychainCredentialSource(IProcessRunner runner) : ICredentialSource
{
    private const string Security = "/usr/bin/security";

    /// <summary>The exit code of <c>security</c> when the item does not exist.</summary>
    private const int ItemNotFound = 44;

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    public async Task<CredentialReadResult> ReadAsync(Account account, AccountPaths paths, CancellationToken ct)
    {
        var spec = new ProcessSpec(
            Security,
            ["find-generic-password", "-s", KeychainServiceName.For(paths.ClaudeConfigDir), "-w"],
            WorkingDirectory: null,
            ReadOnlyDictionary<string, string>.Empty,
            EnvironmentScrub.Always);
        ProcessResult result;
        try
        {
            result = await runner.RunAsync(spec, Timeout, ct).ConfigureAwait(false);
        }
        catch (LaunchException)
        {
            return new CredentialReadResult(CredentialReadStatus.Error, null);
        }

        if (result.ExitCode == ItemNotFound)
        {
            return new CredentialReadResult(CredentialReadStatus.NotLoggedIn, null);
        }

        if (result.TimedOut || result.ExitCode != 0)
        {
            return new CredentialReadResult(CredentialReadStatus.Error, null);
        }

        return CredentialsParser.Parse(result.StdOut) is { } credentials
            ? new CredentialReadResult(CredentialReadStatus.Found, credentials)
            : new CredentialReadResult(CredentialReadStatus.Error, null);
    }
}
