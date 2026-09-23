using Mcacc.Core.Processes;

namespace Mcacc.Core.Abstractions;

public interface IProcessRunner
{
    /// <summary>Starts the process and does not wait for it.</summary>
    /// <exception cref="LaunchException">The process could not be started.</exception>
    void Start(ProcessSpec spec);

    Task<ProcessResult> RunAsync(ProcessSpec spec, TimeSpan timeout, CancellationToken ct);
}
