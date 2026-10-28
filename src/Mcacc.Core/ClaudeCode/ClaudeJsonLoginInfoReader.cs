using Mcacc.Core.Providers;
using Mcacc.Core.Abstractions;
using Mcacc.Core.Model;
using Mcacc.Core.Paths;

namespace Mcacc.Core.ClaudeCode;

public sealed class ClaudeJsonLoginInfoReader : ILoginInfoReader
{
    public AccountLoginInfo? Read(Account account, AccountPaths paths) => ClaudeJsonLoginReader.Read(paths.IdentityFile);
}
