using System.Text.Json.Nodes;
using Mcacc.Core.ClaudeCode;
using Mcacc.Core.Tests.TestSupport;
using Shouldly;
using Xunit;

namespace Mcacc.Core.Tests.ClaudeCode;

public sealed class StatusLineSettingsTests : IDisposable
{
    private const string Command = "C:/Tools/MCACC.exe --mcacc-statusline";

    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    private string File => Path.Join(_temp.Path, "claude", StatusLineSettings.FileName);

    private void Seed(string json)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(File)!);
        System.IO.File.WriteAllText(File, json);
    }

    private JsonObject Saved() => JsonNode.Parse(System.IO.File.ReadAllText(File))!.AsObject();

    [Theory]
    [InlineData(@"C:\Tools\MCACC.exe", "C:/Tools/MCACC.exe --mcacc-statusline")]
    [InlineData("/Applications/MCACC.app/Contents/MacOS/MCACC", "/Applications/MCACC.app/Contents/MacOS/MCACC --mcacc-statusline")]
    [InlineData(@"C:\Program Files\MCACC\MCACC.exe", "'C:/Program Files/MCACC/MCACC.exe' --mcacc-statusline")]
    public void The_command_is_one_both_shells_read(string path, string expected) =>
        StatusLineSettings.Command(path).ShouldBe(expected);

    [Fact]
    public void A_missing_file_is_created_with_the_entry()
    {
        StatusLineSettings.Apply(File, Command).ShouldBe(StatusLineEdit.Written);

        Saved()["statusLine"]!.ToJsonString().ShouldBe("""{"type":"command","command":"C:/Tools/MCACC.exe --mcacc-statusline"}""");
    }

    [Fact]
    public void The_other_keys_stay()
    {
        Seed("""
            {
              // a comment Claude Code itself would not write
              "model": "opus",
              "permissions": { "allow": ["Bash(git status)"] },
              "env": { "NAME": "Grüße" },
            }
            """);

        StatusLineSettings.Apply(File, Command).ShouldBe(StatusLineEdit.Written);

        var saved = Saved();
        saved["model"]!.GetValue<string>().ShouldBe("opus");
        saved["permissions"]!["allow"]![0]!.GetValue<string>().ShouldBe("Bash(git status)");
        System.IO.File.ReadAllText(File).ShouldContain("Grüße");
    }

    [Fact]
    public void A_foreign_status_line_is_never_replaced()
    {
        const string foreign = """{ "statusLine": { "type": "command", "command": "npx ccstatusline" } }""";
        Seed(foreign);

        StatusLineSettings.Apply(File, Command).ShouldBe(StatusLineEdit.Foreign);
        StatusLineSettings.Apply(File, null).ShouldBe(StatusLineEdit.Foreign);

        System.IO.File.ReadAllText(File).ShouldBe(foreign);
    }

    [Fact]
    public void An_own_entry_with_an_old_path_is_corrected()
    {
        Seed("""{ "statusLine": { "type": "command", "command": "D:/Old/MCACC.exe --mcacc-statusline" } }""");

        StatusLineSettings.Apply(File, Command).ShouldBe(StatusLineEdit.Written);

        Saved()["statusLine"]!["command"]!.GetValue<string>().ShouldBe(Command);
    }

    [Fact]
    public void The_same_entry_again_writes_nothing()
    {
        StatusLineSettings.Apply(File, Command);
        var before = System.IO.File.GetLastWriteTimeUtc(File);
        System.IO.File.SetLastWriteTimeUtc(File, before.AddHours(-1));

        StatusLineSettings.Apply(File, Command).ShouldBe(StatusLineEdit.Unchanged);

        System.IO.File.GetLastWriteTimeUtc(File).ShouldBe(before.AddHours(-1));
    }

    [Fact]
    public void Switching_off_takes_only_the_own_entry_out()
    {
        Seed("""{ "model": "opus", "statusLine": { "type": "command", "command": "C:/Tools/MCACC.exe --mcacc-statusline" } }""");

        StatusLineSettings.Apply(File, null).ShouldBe(StatusLineEdit.Removed);

        Saved().ToJsonString().ShouldBe("""{"model":"opus"}""");
    }

    [Fact]
    public void Switching_off_without_a_file_creates_none()
    {
        StatusLineSettings.Apply(File, null).ShouldBe(StatusLineEdit.Unchanged);

        System.IO.File.Exists(File).ShouldBeFalse();
    }

    [Theory]
    [InlineData("[1, 2]")]
    [InlineData("{ broken")]
    [InlineData("")]
    public void A_file_that_is_no_json_object_stays_untouched(string content)
    {
        Seed(content);

        StatusLineSettings.Apply(File, Command).ShouldBe(StatusLineEdit.Unreadable);

        System.IO.File.ReadAllText(File).ShouldBe(content);
    }
}
