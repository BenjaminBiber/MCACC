using Mcacc.Core.Providers;
using Mcacc.Core.Abstractions;
using Mcacc.Core.ClaudeCode;
using Mcacc.Core.Model;
using Mcacc.Core.Paths;

namespace Mcacc.Core.Providers;

public sealed class RoutingLoginInfoReader(ProviderMap<ILoginInfoReader> readers) : ILoginInfoReader
{
    public AccountLoginInfo? Read(Account account, AccountPaths paths) => readers.For(account).Read(account, paths);
}
