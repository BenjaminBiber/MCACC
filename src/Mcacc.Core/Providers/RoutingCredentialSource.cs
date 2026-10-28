using Mcacc.Core.Abstractions;
using Mcacc.Core.ClaudeCode;
using Mcacc.Core.Model;
using Mcacc.Core.Paths;

namespace Mcacc.Core.Providers;

/// <summary>Reads the token with the source that belongs to the account's provider.</summary>
public sealed class RoutingCredentialSource(ProviderMap<ICredentialSource> sources) : ICredentialSource
{
    public Task<CredentialReadResult> ReadAsync(Account account, AccountPaths paths, CancellationToken ct) =>
        sources.For(account).ReadAsync(account, paths, ct);
}
