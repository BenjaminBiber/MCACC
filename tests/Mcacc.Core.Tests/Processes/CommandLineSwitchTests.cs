using Mcacc.Core.Processes;
using Shouldly;
using Xunit;

namespace Mcacc.Core.Tests.Processes;

public class CommandLineSwitchTests
{
    private const string Exe = @"""C:\Program Files\WindowsApps\Claude_2.7032.0.0_x64__pzs8sxrjxfjjc\app\Claude.exe""";

    [Theory]
    [InlineData(@"--user-data-dir=""C:\a b\c""", @"C:\a b\c")]
    [InlineData(@"""--user-data-dir=C:\a b\c""", @"C:\a b\c")]
    [InlineData(@"--user-data-dir=C:\x", @"C:\x")]
    [InlineData("--user-data-dir=/Users/x/desk top ä --other", "/Users/x/desk top ä")]
    public void Reads_the_forms_from_the_brief(string commandLine, string expected)
    {
        CommandLineSwitch.TryGetValue(commandLine, "--user-data-dir", out var value).ShouldBeTrue();

        value.ShouldBe(expected);
    }

    [Theory]
    [InlineData(Exe + @" --type=gpu-process --user-data-dir=""C:\Users\USER\AppData\Roaming\Claude"" --gpu-preferences=UAAAAAAAAADgAAAEAAAAAAAAAAAAAAAAAABgAAEAAAA4AAAAAAAAAAAAAAAEAAAAAAAAABAAAAAAAAAAEAAAAAAAAAAIAAAAAAAAAAgAAAAAAAAA --field-trial-handle=1968,i,5137393027375812345,11400735125432765432,262144 /prefetch:2")]
    [InlineData(Exe + @" ""--user-data-dir=C:\Users\USER\AppData\Roaming\Claude"" --type=renderer")]
    [InlineData(Exe + @" --type=renderer --lang=de --user-data-dir=C:\Users\USER\AppData\Roaming\Claude")]
    public void Reads_the_value_between_other_arguments(string commandLine)
    {
        CommandLineSwitch.TryGetValue(commandLine, "--user-data-dir", out var value).ShouldBeTrue();

        value.ShouldBe(@"C:\Users\USER\AppData\Roaming\Claude");
    }

    [Fact]
    public void Unquoted_windows_value_ends_before_a_slash_switch_like_the_crashpad_prefetch_argument()
    {
        var commandLine = Exe + @" --type=crashpad-handler --user-data-dir=C:\Users\USER\AppData\Roaming\Claude /prefetch:4 --no-rate-limit";

        CommandLineSwitch.TryGetValue(commandLine, "--user-data-dir", out var value).ShouldBeTrue();

        value.ShouldBe(@"C:\Users\USER\AppData\Roaming\Claude");
    }

    [Theory]
    [InlineData("/Applications/Claude.app/Contents/MacOS/Claude --user-data-dir=/Users/USER/Library/Application Support/Claude --lang=de", "/Users/USER/Library/Application Support/Claude")]
    [InlineData("/Applications/Claude.app/Contents/MacOS/Claude --user-data-dir=/Users/USER/desk top ä", "/Users/USER/desk top ä")]
    [InlineData("/Applications/Claude.app/Contents/MacOS/Claude --user-data-dir=/Users/USER/a  b --x", "/Users/USER/a  b")]
    public void Unquoted_value_with_spaces_runs_to_the_next_switch_or_the_end(string commandLine, string expected)
    {
        CommandLineSwitch.TryGetValue(commandLine, "--user-data-dir", out var value).ShouldBeTrue();

        value.ShouldBe(expected);
    }

    [Theory]
    [InlineData(@"--user-data-dir=C:\x   ", @"C:\x")]
    [InlineData("--user-data-dir=/Users/x \t --other", "/Users/x")]
    [InlineData(@"--user-data-dir="" C:\a b\c "" --x", @"C:\a b\c")]
    [InlineData(@"--user-data-dir=""C:\a b\c", @"C:\a b\c")]
    public void Trims_surrounding_whitespace_and_quotes(string commandLine, string expected)
    {
        CommandLineSwitch.TryGetValue(commandLine, "--user-data-dir", out var value).ShouldBeTrue();

        value.ShouldBe(expected);
    }

    [Fact]
    public void First_occurrence_wins()
    {
        CommandLineSwitch.TryGetValue("--user-data-dir=/a --user-data-dir=/b", "--user-data-dir", out var value).ShouldBeTrue();

        value.ShouldBe("/a");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(Exe)]
    [InlineData(Exe + " --type=renderer --lang=de")]
    [InlineData(Exe + @" --user-data-dir C:\x")]
    [InlineData(Exe + @" --old-user-data-dir=C:\x")]
    [InlineData(Exe + @" --User-Data-Dir=C:\x")]
    [InlineData(@"C:\tools\x--user-data-dir=C:\x")]
    public void Returns_false_when_the_switch_is_absent(string? commandLine)
    {
        CommandLineSwitch.TryGetValue(commandLine, "--user-data-dir", out var value).ShouldBeFalse();

        value.ShouldBe("");
    }

    [Theory]
    [InlineData("--user-data-dir=")]
    [InlineData("--user-data-dir= --type=renderer")]
    [InlineData(@"--user-data-dir="""" --type=renderer")]
    [InlineData(@"""--user-data-dir=""")]
    public void Returns_false_for_an_empty_value(string commandLine)
    {
        CommandLineSwitch.TryGetValue(commandLine, "--user-data-dir", out var value).ShouldBeFalse();

        value.ShouldBe("");
    }

    [Fact]
    public void Works_for_other_switch_names()
    {
        CommandLineSwitch.TryGetValue(Exe + " --type=gpu-process --lang=de", "--type", out var value).ShouldBeTrue();

        value.ShouldBe("gpu-process");
    }
}
