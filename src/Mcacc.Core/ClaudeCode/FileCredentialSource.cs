using Mcacc.Core.Abstractions;
using Mcacc.Core.Model;
using Mcacc.Core.Paths;

namespace Mcacc.Core.ClaudeCode;

/// <summary>The Windows and Linux token source: <c>&lt;claudeDir&gt;/.credentials.json</c>.</summary>
public sealed class FileCredentialSource : ICredentialSource
{
    public async Task<CredentialReadResult> ReadAsync(Account account, AccountPaths paths, CancellationToken ct)
    {
        string json;
        try
        {
            json = await File.ReadAllTextAsync(paths.CredentialsFile, ct).ConfigureAwait(false);
        }
        catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException)
        {
            return new CredentialReadResult(CredentialReadStatus.NotLoggedIn, null);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return new CredentialReadResult(CredentialReadStatus.Error, null);
        }

        return CredentialsParser.Parse(json) is { } credentials
            ? new CredentialReadResult(CredentialReadStatus.Found, credentials)
            : new CredentialReadResult(CredentialReadStatus.Error, null);
    }
}
