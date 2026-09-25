using Mcacc.Core.Logging;
using Shouldly;
using Xunit;

namespace Mcacc.App.Tests;

/// <summary>
/// The pipe name depends on <c>MCACC_HOME</c>; the test sets its own, so it cannot reach an MCACC that runs on this
/// machine.
/// </summary>
public sealed class SingleInstanceTests : IDisposable
{
    private readonly string? _previousHome = Environment.GetEnvironmentVariable("MCACC_HOME");

    public SingleInstanceTests() =>
        Environment.SetEnvironmentVariable("MCACC_HOME", Path.Join(Path.GetTempPath(), $"mcacc-pipe-{Guid.NewGuid():N}"));

    public void Dispose() => Environment.SetEnvironmentVariable("MCACC_HOME", _previousHome);

    [Fact]
    public async Task A_second_start_reaches_the_first_one()
    {
        var activated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using (SingleInstance.Listen(() => activated.TrySetResult(), NullLog.Instance))
        {
            SingleInstance.SignalExisting().ShouldBeTrue();

            await activated.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public void Without_a_first_start_the_signal_goes_nowhere()
    {
        SingleInstance.SignalExisting().ShouldBeFalse();
    }

    [Fact]
    public void The_pipe_depends_on_the_data_folder()
    {
        var first = SingleInstance.PipeName();
        Environment.SetEnvironmentVariable("MCACC_HOME", Path.Join(Path.GetTempPath(), "mcacc-other"));

        SingleInstance.PipeName().ShouldNotBe(first);
    }
}
