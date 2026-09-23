using System.Collections.ObjectModel;
using Mcacc.Core.Processes;
using Mcacc.Platform.Common;
using Mcacc.Platform.Tests.TestSupport;
using Shouldly;
using Xunit;

namespace Mcacc.Platform.Tests.Common;

[Trait("Category", "Integration")]
public class SystemProcessRunnerTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(20);

    private readonly SystemProcessRunner _runner = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static ProcessSpec Spec(
        string fileName,
        IReadOnlyList<string> arguments,
        IReadOnlyDictionary<string, string>? envSet = null,
        IReadOnlyList<string>? envUnset = null) =>
        new(fileName, arguments, WorkingDirectory: null, envSet ?? ReadOnlyDictionary<string, string>.Empty, envUnset ?? []);

    private static ProcessSpec Echo(string text) => OperatingSystem.IsWindows()
        ? Spec("cmd.exe", ["/c", "echo", text])
        : Spec("/bin/sh", ["-c", $"echo {text}"]);

    private static ProcessSpec EchoVariable(string name) => OperatingSystem.IsWindows()
        ? Spec("cmd.exe", ["/c", $"echo %{name}%"])
        : Spec("/bin/sh", ["-c", $"echo ${name}"]);

    [Fact]
    public async Task Runs_a_command_and_returns_its_output()
    {
        var result = await _runner.RunAsync(Echo("hi"), Timeout, Ct);

        result.TimedOut.ShouldBeFalse();
        result.ExitCode.ShouldBe(0);
        result.StdOut.Trim().ShouldBe("hi");
    }

    [Fact]
    public async Task Applies_env_set_to_the_child()
    {
        var spec = EchoVariable("MCACC_TEST") with
        {
            EnvSet = new Dictionary<string, string> { ["MCACC_TEST"] = "set-by-mcacc" },
        };

        var result = await _runner.RunAsync(spec, Timeout, Ct);

        result.StdOut.Trim().ShouldBe("set-by-mcacc");
    }

    [Fact]
    public async Task Removes_env_unset_from_the_child()
    {
        Environment.SetEnvironmentVariable("MCACC_TEST_UNSET", "inherited");
        try
        {
            var spec = EchoVariable("MCACC_TEST_UNSET") with { EnvUnset = ["MCACC_TEST_UNSET"] };

            var result = await _runner.RunAsync(spec, Timeout, Ct);

            result.StdOut.Trim().ShouldNotBe("inherited");
        }
        finally
        {
            Environment.SetEnvironmentVariable("MCACC_TEST_UNSET", null);
        }
    }

    [Fact]
    public async Task Reports_the_exit_code_of_a_failing_command()
    {
        var spec = OperatingSystem.IsWindows()
            ? Spec("cmd.exe", ["/c", "exit", "3"])
            : Spec("/bin/sh", ["-c", "exit 3"]);

        (await _runner.RunAsync(spec, Timeout, Ct)).ExitCode.ShouldBe(3);
    }

    [Fact]
    public async Task Kills_a_command_that_outlasts_the_timeout()
    {
        var spec = OperatingSystem.IsWindows()
            ? Spec("cmd.exe", ["/c", "pause"])
            : Spec("/bin/sh", ["-c", "sleep 30"]);

        var result = await _runner.RunAsync(spec, TimeSpan.FromMilliseconds(300), Ct);

        result.TimedOut.ShouldBeTrue();
    }

    [Fact]
    public async Task An_unknown_executable_fails_to_start()
    {
        var spec = Spec("mcacc-does-not-exist.exe", []);

        await Should.ThrowAsync<LaunchException>(() => _runner.RunAsync(spec, Timeout, Ct));
        Should.Throw<LaunchException>(() => _runner.Start(spec));
    }

    // Only ASCII: cmd echoes in the console code page, which would garble an umlaut in the captured output.
    [WindowsFact]
    public async Task Passes_an_argument_with_spaces_and_shell_characters_as_one_argument()
    {
        var spec = Spec("cmd.exe", ["/c", "echo", @"O'Neil 100%;$x & y"]);

        var result = await _runner.RunAsync(spec, Timeout, Ct);

        result.StdOut.Trim().ShouldBe(@"""O'Neil 100%;$x & y""");
    }
}
