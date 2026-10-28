using Mcacc.Core.Providers;
using Mcacc.Core.ClaudeCode;
using Mcacc.Core.Model;
using Mcacc.Core.Paths;

namespace Mcacc.Core.Abstractions;

public interface ILoginInfoReader
{
    /// <returns>Null when the account has no login yet.</returns>
    AccountLoginInfo? Read(Account account, AccountPaths paths);
}
