using Mcacc.Core.Model;
using Mcacc.Core.Processes;

namespace Mcacc.Core.Abstractions;

public interface IProcessSnapshotProvider
{
    /// <summary>The running processes that could belong to that provider's desktop app, by name alone.</summary>
    Task<IReadOnlyList<ProcessInfo>> GetProcessesAsync(ProviderDescriptor provider, CancellationToken ct);
}
