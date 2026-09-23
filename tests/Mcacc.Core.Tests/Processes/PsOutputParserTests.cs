using Mcacc.Core.Processes;
using Mcacc.Core.Tests.TestSupport;
using Shouldly;
using Xunit;

namespace Mcacc.Core.Tests.Processes;

public class PsOutputParserTests
{
    private const string Main = "/Applications/Claude.app/Contents/MacOS/Claude";
    private const string Renderer =
        "/Applications/Claude.app/Contents/Frameworks/Claude Helper (Renderer).app/Contents/MacOS/Claude Helper (Renderer)";

    private static DateTimeOffset Local(int month, int day, int hour, int minute, int second) =>
        new(new DateTime(2026, month, day, hour, minute, second, DateTimeKind.Local));

    [Fact]
    public void Parses_pid_parent_start_time_exe_and_command()
    {
        var command = Main + " --user-data-dir=/Users/USER/.mcacc/profiles/privat/desktop";

        var processes = PsOutputParser.Parse($"  812     1 Wed Sep 23 10:00:00 2026 {command}\n");

        processes.ShouldHaveSingleItem().ShouldBe(new ProcessInfo(812, 1, Main, command, Local(9, 23, 10, 0, 0)));
    }

    [Fact]
    public void Exe_with_spaces_runs_up_to_the_first_switch()
    {
        var processes = PsOutputParser.Parse($"824 812 Wed Sep 23 10:00:02 2026 {Renderer} --type=renderer --lang=de");

        processes.ShouldHaveSingleItem().ExePath.ShouldBe(Renderer);
    }

    [Fact]
    public void Command_without_switches_is_the_exe()
    {
        var processes = PsOutputParser.Parse($"812 1 Wed Sep 23 10:00:00 2026 {Main}");

        processes.ShouldHaveSingleItem().ExePath.ShouldBe(Main);
    }

    [Fact]
    public void Keeps_the_spacing_inside_the_command_and_drops_trailing_whitespace()
    {
        var command = Main + " --user-data-dir=/Users/USER/a  b --lang=de";

        var process = PsOutputParser.Parse($"812 1 Wed Sep 23 10:00:00 2026    {command}  \r\n").ShouldHaveSingleItem();

        process.CommandLine.ShouldBe(command);
    }

    [Fact]
    public void Parses_a_space_padded_day()
    {
        var process = PsOutputParser.Parse("  377   360 Thu Sep  3 08:12:44 2026 claude").ShouldHaveSingleItem();

        process.StartTime.ShouldBe(Local(9, 3, 8, 12, 44));
        process.CommandLine.ShouldBe("claude");
    }

    [Theory]
    [InlineData("Mi. Sep 23 10:00:00 2026")]
    [InlineData("Thu Sep 23 10:00:00 2026")]
    [InlineData("Wed Sep 31 10:00:00 2026")]
    public void Unparseable_start_time_gives_null_and_keeps_the_rest(string startTime)
    {
        var process = PsOutputParser.Parse($"812 1 {startTime} {Main} --lang=de").ShouldHaveSingleItem();

        process.ShouldBe(new ProcessInfo(812, 1, Main, Main + " --lang=de", StartTime: null));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("  PID  PPID STARTED                  COMMAND")]
    [InlineData("abc 1 Wed Sep 23 10:00:00 2026 /bin/x")]
    [InlineData("812 x Wed Sep 23 10:00:00 2026 /bin/x")]
    [InlineData("-812 1 Wed Sep 23 10:00:00 2026 /bin/x")]
    [InlineData("99999999999 1 Wed Sep 23 10:00:00 2026 /bin/x")]
    [InlineData("812 1 Wed Sep 23 10:00:00 2026")]
    [InlineData("812 1 Wed Sep 23 10:00:00 2026   ")]
    [InlineData("812 1 Wed Sep 23")]
    public void Skips_a_malformed_line(string line)
    {
        PsOutputParser.Parse(line).ShouldBeEmpty();
    }

    [Fact]
    public void Skipping_a_malformed_line_keeps_the_others()
    {
        var output = "812 1 Wed Sep 23 10:00:00 2026 /bin/a\r\n\r\nnot a process line\r\n815 812 Wed Sep 23 10:00:01 2026 /bin/b\r\n";

        PsOutputParser.Parse(output).Select(p => p.Pid).ShouldBe([812, 815]);
    }

    [Fact]
    public void Parses_every_line_of_the_profile_fixture()
    {
        var processes = PsOutputParser.Parse(FixtureFiles.ReadAllText("ps-profile.txt"));

        processes.Select(p => p.Pid).ShouldBe([377, 812, 815, 818, 820, 824, 901, 1204, 1207, 1210, 1333]);
        processes.ShouldAllBe(p => p.StartTime != null);
        processes.Single(p => p.Pid == 824).ExePath.ShouldBe(Renderer);
        processes.Single(p => p.Pid == 1204).CommandLine.ShouldBe(Main + " --user-data-dir=/Users/USER/desk top ä");
    }
}
