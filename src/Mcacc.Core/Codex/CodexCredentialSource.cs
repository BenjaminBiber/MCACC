using Mcacc.Core.Abstractions;
using Mcacc.Core.ClaudeCode;
using Mcacc.Core.Model;
using Mcacc.Core.Paths;

namespace Mcacc.Core.Codex;

/// <summary>
/// The Codex token source: <c>auth.json</c>, the same file the login reader uses. It never writes and never
/// refreshes — an expired token simply leaves the last known limits standing.
/// </summary>
public sealed class CodexCredentialSource : ICredentialSource
{
    public Task<CredentialReadResult> ReadAsync(Account account, AccountPaths paths, CancellationToken ct)
    {
        var (outcome, auth) = CodexAuthFileReader.Read(paths.CredentialsFile);
        return Task.FromResult(Result(outcome, auth));
    }

    private static CredentialReadResult Result(CodexAuthFileReader.Outcome outcome, CodexAuth? auth) => outcome switch
    {
        CodexAuthFileReader.Outcome.Missing => new CredentialReadResult(CredentialReadStatus.NotLoggedIn, null),
        CodexAuthFileReader.Outcome.Found when auth?.AccessToken is { } token => new CredentialReadResult(
            CredentialReadStatus.Found,
            new OAuthCredentials(token, auth.AccessTokenExpiresAt, auth.Login?.PlanType)),

        // An API-key login has no OAuth token, so there is nothing to ask the limits endpoint with.
        CodexAuthFileReader.Outcome.Found => new CredentialReadResult(CredentialReadStatus.NotLoggedIn, null),
        _ => new CredentialReadResult(CredentialReadStatus.Error, null),
    };
}
