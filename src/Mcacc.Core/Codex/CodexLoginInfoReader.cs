using Mcacc.Core.Abstractions;
using Mcacc.Core.Model;
using Mcacc.Core.Paths;
using Mcacc.Core.Providers;

namespace Mcacc.Core.Codex;

/// <summary>
/// The logged-in Codex account, out of the id token in <c>auth.json</c>. A login with an API key carries no
/// e-mail address, so it reports a login whose <see cref="AccountLoginInfo.Email"/> is null — not "no login".
/// </summary>
public sealed class CodexLoginInfoReader : ILoginInfoReader
{
    public AccountLoginInfo? Read(Account account, AccountPaths paths)
    {
        var (outcome, auth) = CodexAuthFileReader.Read(paths.IdentityFile);
        if (outcome != CodexAuthFileReader.Outcome.Found || auth is null)
        {
            return null;
        }

        if (auth.Login is { } login)
        {
            return login;
        }

        return auth.HasApiKey
            ? new AccountLoginInfo(Email: null, OrganizationName: null, DisplayName: null, AccountUuid: auth.AccountId)
            : null;
    }
}
