using Mcacc.Core.ClaudeCode;
using Mcacc.Core.Paths;

namespace Mcacc.Core.Abstractions;

public interface ILoginInfoReader
{
    /// <returns>Null when the account has no login yet.</returns>
    ClaudeLoginInfo? Read(AccountPaths paths);
}
