using Mcacc.Core.Abstractions;
using Mcacc.Core.Processes;

namespace Mcacc.Core.Tests.TestSupport;

internal sealed class FakeProcessRunner : IProcessRunner
{
    /// <summary>Every start request, including the ones <see cref="OnStart"/> fails.</summary>
    public List<ProcessSpec> Started { get; } = [];

    /// <summary>Runs after a start is recorded; throw from it to fail the start.</summary>
    public Action<ProcessSpec>? OnStart { get; set; }

    public void Start(ProcessSpec spec)
    {
        Started.Add(spec);
        OnStart?.Invoke(spec);
    }

    public Task<ProcessResult> RunAsync(ProcessSpec spec, TimeSpan timeout, CancellationToken ct) =>
        throw new NotSupportedException("The launch services never wait for a process.");
}
