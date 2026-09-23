using Mcacc.Core.ClaudeCode;
using Mcacc.Core.Model;
using Mcacc.Core.Paths;

namespace Mcacc.Core.Abstractions;

/// <summary>Reads the Claude Code OAuth token of an account. It never writes and never refreshes.</summary>
public interface ICredentialSource
{
    Task<CredentialReadResult> ReadAsync(Account account, AccountPaths paths, CancellationToken ct);
}
