using Mcacc.Core.ConfigCopy;
using Shouldly;
using Xunit;

namespace Mcacc.Core.Tests.ConfigCopy;

public class SettingsPathRewriterTests
{
    private const string WinSource = @"C:\Users\x\.claude";
    private const string WinTarget = @"C:\Users\x\.mcacc\profiles\privat\claude";
    private const string PosixSource = "/Users/x/.claude";
    private const string PosixTarget = "/Users/x/.mcacc/profiles/privat/claude";

    private static string RewriteWindows(string json, bool sourceIsDefault = true) =>
        SettingsPathRewriter.Rewrite(json, WinSource, WinTarget, sourceIsDefault);

    [Fact]
    public void Raw_windows_path_is_replaced_with_the_raw_target()
    {
        RewriteWindows(@"C:\Users\x\.claude\hooks\a.ps1")
            .ShouldBe(@"C:\Users\x\.mcacc\profiles\privat\claude\hooks\a.ps1");
    }

    [Fact]
    public void Json_escaped_path_is_replaced_with_the_json_escaped_target()
    {
        RewriteWindows("""{ "command": "C:\\Users\\x\\.claude\\hooks\\a.ps1" }""")
            .ShouldBe("""{ "command": "C:\\Users\\x\\.mcacc\\profiles\\privat\\claude\\hooks\\a.ps1" }""");
    }

    [Fact]
    public void Forward_slash_path_is_replaced_with_the_forward_slash_target()
    {
        RewriteWindows("""{ "command": "C:/Users/x/.claude/hooks/a.sh" }""")
            .ShouldBe("""{ "command": "C:/Users/x/.mcacc/profiles/privat/claude/hooks/a.sh" }""");
    }

    [Theory]
    [InlineData("""c:\\users\\X\\.CLAUDE\\hooks""", """C:\\Users\\x\\.mcacc\\profiles\\privat\\claude\\hooks""")]
    [InlineData("c:/USERS/x/.Claude/hooks", "C:/Users/x/.mcacc/profiles/privat/claude/hooks")]
    [InlineData("~/.CLAUDE/hooks", "C:/Users/x/.mcacc/profiles/privat/claude/hooks")]
    [InlineData("$home/.claude/hooks", "C:/Users/x/.mcacc/profiles/privat/claude/hooks")]
    public void Matching_ignores_case(string json, string expected)
    {
        RewriteWindows(json).ShouldBe(expected);
    }

    [Theory]
    [InlineData("bash ~/.claude/hooks/a.sh")]
    [InlineData("bash $HOME/.claude/hooks/a.sh")]
    public void Home_relative_forms_of_the_default_source_become_the_forward_slash_target(string json)
    {
        RewriteWindows(json).ShouldBe("bash C:/Users/x/.mcacc/profiles/privat/claude/hooks/a.sh");
    }

    [Theory]
    [InlineData("bash ~/.claude/hooks/a.sh")]
    [InlineData("bash $HOME/.claude/hooks/a.sh")]
    public void Home_relative_forms_are_kept_when_the_source_is_not_the_default(string json)
    {
        RewriteWindows(json, sourceIsDefault: false).ShouldBe(json);
    }

    [Theory]
    [InlineData("")]
    [InlineData("/x")]
    [InlineData(@"\x")]
    [InlineData("\"")]
    [InlineData("'")]
    [InlineData(" x")]
    [InlineData("\tx")]
    [InlineData("\n")]
    public void Match_followed_by_a_boundary_is_replaced(string after)
    {
        RewriteWindows("C:/Users/x/.claude" + after).ShouldBe("C:/Users/x/.mcacc/profiles/privat/claude" + after);
        RewriteWindows("~/.claude" + after).ShouldBe("C:/Users/x/.mcacc/profiles/privat/claude" + after);
    }

    [Theory]
    [InlineData(@"C:\Users\x\.claude.json")]
    [InlineData("""C:\\Users\\x\\.claude.json""")]
    [InlineData("C:/Users/x/.claude.json")]
    [InlineData("C:/Users/x/.claude-x/hooks")]
    [InlineData("C:/Users/x/.claudex")]
    [InlineData("C:/Users/x/.claude_backup")]
    [InlineData("C:/Users/x/.claude:1")]
    [InlineData("~/.claude.json")]
    [InlineData("~/.claude-x")]
    [InlineData("$HOME/.claude.json")]
    [InlineData("$HOME/.claude-x/hooks")]
    [InlineData("C:/Users/xy/.claude/hooks")]
    [InlineData("D:/Users/x/.claude/hooks")]
    public void Other_paths_stay_untouched(string json)
    {
        RewriteWindows(json).ShouldBe(json);
    }

    [Fact]
    public void Posix_source_is_replaced_once_with_all_forms()
    {
        const string json = """
            { "hooks": "/Users/x/.claude/hooks/a.sh", "status": "~/.claude/statusline.sh",
              "other": "$HOME/.claude", "keep": "/Users/x/.claude.json" }
            """;

        SettingsPathRewriter.Rewrite(json, PosixSource, PosixTarget, sourceIsDefault: true).ShouldBe("""
            { "hooks": "/Users/x/.mcacc/profiles/privat/claude/hooks/a.sh", "status": "/Users/x/.mcacc/profiles/privat/claude/statusline.sh",
              "other": "/Users/x/.mcacc/profiles/privat/claude", "keep": "/Users/x/.claude.json" }
            """);
    }

    [Fact]
    public void Profile_source_is_replaced_in_every_spelling()
    {
        const string source = @"C:\Users\x\.mcacc\profiles\firma\claude";
        const string json = """
            { "a": "C:\\Users\\x\\.mcacc\\profiles\\firma\\claude\\hooks",
              "b": "C:/Users/x/.mcacc/profiles/firma/claude/hooks",
              "c": "~/.claude/hooks" }
            """;

        SettingsPathRewriter.Rewrite(json, source, WinTarget, sourceIsDefault: false).ShouldBe("""
            { "a": "C:\\Users\\x\\.mcacc\\profiles\\privat\\claude\\hooks",
              "b": "C:/Users/x/.mcacc/profiles/privat/claude/hooks",
              "c": "~/.claude/hooks" }
            """);
    }

    [Fact]
    public void Every_form_in_a_realistic_settings_file_is_replaced_exactly_once()
    {
        const string json = """
            {
              "statusLine": { "type": "command", "command": "bash ~/.claude/statusline.sh" },
              "hooks": {
                "Stop": [ { "hooks": [ { "type": "command", "command": "pwsh -File C:\\Users\\x\\.claude\\hooks\\stop.ps1" } ] } ],
                "PreToolUse": [ { "hooks": [ { "type": "command", "command": "node 'C:/Users/x/.claude/hooks/guard.js'" } ] } ]
              },
              "env": { "NOTES": "$HOME/.claude/notes" },
              "permissions": { "allow": [ "Read(C:/Users/x/.claude.json)" ] }
            }
            """;

        RewriteWindows(json).ShouldBe("""
            {
              "statusLine": { "type": "command", "command": "bash C:/Users/x/.mcacc/profiles/privat/claude/statusline.sh" },
              "hooks": {
                "Stop": [ { "hooks": [ { "type": "command", "command": "pwsh -File C:\\Users\\x\\.mcacc\\profiles\\privat\\claude\\hooks\\stop.ps1" } ] } ],
                "PreToolUse": [ { "hooks": [ { "type": "command", "command": "node 'C:/Users/x/.mcacc/profiles/privat/claude/hooks/guard.js'" } ] } ]
              },
              "env": { "NOTES": "C:/Users/x/.mcacc/profiles/privat/claude/notes" },
              "permissions": { "allow": [ "Read(C:/Users/x/.claude.json)" ] }
            }
            """);
    }

    [Fact]
    public void Regex_and_replacement_metacharacters_in_paths_are_literal()
    {
        const string source = @"C:\Users\Max (Test)+1\.claude";
        const string target = @"C:\Users\a$1b\.mcacc\profiles\privat\claude";

        SettingsPathRewriter.Rewrite("C:/Users/Max (Test)+1/.claude/x C:/Users/Max Test1/.claude/x", source, target, false)
            .ShouldBe("C:/Users/a$1b/.mcacc/profiles/privat/claude/x C:/Users/Max Test1/.claude/x");
    }

    [Fact]
    public void Json_without_references_is_returned_unchanged()
    {
        const string json = """{ "model": "opus", "theme": "dark" }""";

        RewriteWindows(json).ShouldBe(json);
    }
}
