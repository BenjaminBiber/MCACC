using Mcacc.Core.Processes;

namespace Mcacc.Core.Abstractions;

public interface IProcessSnapshotProvider
{
    Task<IReadOnlyList<ProcessInfo>> GetClaudeProcessesAsync(CancellationToken ct);
}
