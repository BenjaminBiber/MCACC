using Mcacc.Core.Processes;
using Mcacc.Core.Terminal;
using Mcacc.Core.Tests.TestSupport;
using Shouldly;
using Xunit;

namespace Mcacc.Core.Tests.Terminal;

public class CmdBatchBuilderTests
{
    private static string CrLfLines(params string[] lines) => string.Concat(lines.Select(l => l + "\r\n"));

    [Fact]
    public void Profile_batch_sets_utf8_scrubs_sets_the_config_dir_changes_to_home_and_runs_claude()
    {
        CmdBatchBuilder.Build(LaunchRequests.WindowsProfile()).ShouldBe(CrLfLines(
            "@chcp 65001 >nul",
            "@echo off",
            "set \"CLAUDECODE=\"",
            "set \"CLAUDE_CODE_ENTRYPOINT=\"",
            "set \"CLAUDE_CODE_SSE_PORT=\"",
            @"set ""CLAUDE_CONFIG_DIR=C:\Users\Jö O'Neil’s 100%%;$x\.mcacc\profiles\privat\claude""",
            @"cd /d ""C:\Users\Jö O'Neil’s 100%%;$x""",
            "title Claude Code · Privat",
            "claude",
            "@echo on"));
    }

    [Fact]
    public void Default_batch_clears_the_config_dir_variable()
    {
        var lines = CmdBatchBuilder.Build(LaunchRequests.WindowsDefault()).Split("\r\n");

        lines.ShouldContain("set \"CLAUDE_CONFIG_DIR=\"");
        lines.Count(l => l.Contains("CLAUDE_CONFIG_DIR")).ShouldBe(1);
    }

    [Fact]
    public void Uses_crlf_only_and_ends_with_echo_on()
    {
        var content = CmdBatchBuilder.Build(LaunchRequests.WindowsProfile());

        content.Replace("\r\n", "").ShouldNotContain('\n');
        content.Replace("\r\n", "").ShouldNotContain('\r');
        content.ShouldEndWith("\r\n@echo on\r\n");
    }

    [Fact]
    public void Title_line_holds_the_sanitized_title()
    {
        CmdBatchBuilder.Build(LaunchRequests.WindowsProfile("A&B | \"C\" 50%")).Split("\r\n")
            .ShouldContain("title Claude Code · AB C 50");
    }

    [Theory]
    [InlineData("\"")]
    [InlineData("\r")]
    [InlineData("\n")]
    public void Refuses_a_config_dir_or_home_that_cannot_be_written_into_a_batch_file(string bad)
    {
        var request = LaunchRequests.WindowsProfile();

        Should.Throw<ArgumentException>(() => CmdBatchBuilder.Build(request with { ClaudeConfigDir = @"C:\a" + bad }));
        Should.Throw<ArgumentException>(() => CmdBatchBuilder.Build(request with { HomeDir = @"C:\a" + bad }));
    }

    [Fact]
    public void Written_batch_file_has_no_bom_and_keeps_crlf()
    {
        using var temp = new TempDirectory();
        var path = Path.Join(temp.Path, "privat.cmd");
        var content = CmdBatchBuilder.Build(LaunchRequests.WindowsProfile());

        new LaunchFileWriter().Write(new LaunchFile(path, content, LineEnding.CrLf, Executable: false));

        var bytes = File.ReadAllBytes(path);
        bytes[..16].ShouldBe("@chcp 65001 >nul"u8.ToArray());
        bytes[16..18].ShouldBe("\r\n"u8.ToArray());
        File.ReadAllText(path).ShouldBe(content);
    }
}
