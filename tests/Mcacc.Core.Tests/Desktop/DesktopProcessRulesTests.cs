using Mcacc.Core.Desktop;
using Mcacc.Core.Model;
using Mcacc.Core.Processes;
using Shouldly;
using Xunit;

namespace Mcacc.Core.Tests.Desktop;

public class DesktopProcessRulesTests
{
    private const string MsixExe = @"C:\Program Files\WindowsApps\Claude_2.7032.0.0_x64__pzs8sxrjxfjjc\app\Claude.exe";
    private const string SquirrelExe = @"C:\Users\USER\AppData\Local\AnthropicClaude\app-0.14.10\claude.exe";
    private const string MacMain = "/Applications/Claude.app/Contents/MacOS/Claude";
    private const string MacRenderer =
        "/Applications/Claude.app/Contents/Frameworks/Claude Helper (Renderer).app/Contents/MacOS/Claude Helper (Renderer)";
    private const string MacCrashpad =
        "/Applications/Claude.app/Contents/Frameworks/Electron Framework.framework/Helpers/chrome_crashpad_handler";

    private static readonly DesktopProcessRules Windows = DesktopProcessRules.For(OsKind.Windows, ProviderDescriptors.Claude);
    private static readonly DesktopProcessRules Mac = DesktopProcessRules.For(OsKind.MacOS, ProviderDescriptors.Claude);

    private static ProcessInfo Process(string? exePath, string? commandLine = null) =>
        new(100, 1, exePath, commandLine ?? (exePath is null ? null : $"\"{exePath}\""), StartTime: null);

    [Theory]
    [InlineData(MsixExe)]
    [InlineData(@"c:\program files\windowsapps\claude_2.7032.0.0_x64__pzs8sxrjxfjjc\APP\CLAUDE.EXE")]
    [InlineData(SquirrelExe)]
    [InlineData(@"C:\Users\USER\AppData\Local\AnthropicClaude\app-0.14.10\Claude.exe")]
    public void Windows_desktop_paths_are_desktop_processes(string exePath)
    {
        Windows.IsDesktopProcess(Process(exePath)).ShouldBeTrue();
    }

    [Theory]
    [InlineData(@"C:\Users\USER\AppData\Roaming\Claude\claude-code\2.1.280\claude.exe")]
    [InlineData(@"C:\Users\USER\AppData\Local\Microsoft\WinGet\Packages\Anthropic.ClaudeCode_Microsoft.Winget.Source_8wekyb3d8bbwe\claude.exe")]
    [InlineData(@"C:\Users\USER\.local\bin\claude.exe")]
    [InlineData(@"C:\Users\USER\AppData\Local\Microsoft\WindowsApps\claude-desktop.exe")]
    [InlineData(@"C:\Users\USER\AppData\Local\AnthropicClaude\claude.exe")]
    [InlineData(@"C:\Users\USER\AppData\Local\AnthropicClaude\Update.exe")]
    [InlineData(@"C:\Program Files\WindowsApps\Claude_2.7032.0.0_x64__pzs8sxrjxfjjc\app\resources\claude-code\claude.exe")]
    [InlineData(@"C:\Users\USER\AppData\Local\AnthropicClaude\app-0.14.10\resources\claude-code\claude.exe")]
    [InlineData(@"C:\Program Files\WindowsApps\Claude_2.7032.0.0_x64__pzs8sxrjxfjjc\app\claude-code\app\Claude.exe")]
    [InlineData(@"C:\Program Files\WindowsApps\Other_1.0.0.0_x64__abc\app\Claude.exe")]
    [InlineData(MacMain)]
    [InlineData(null)]
    public void Other_windows_paths_are_not_desktop_processes(string? exePath)
    {
        Windows.IsDesktopProcess(Process(exePath)).ShouldBeFalse();
    }

    [Theory]
    [InlineData(MacMain)]
    [InlineData("/Users/USER/Applications/Claude.app/Contents/MacOS/Claude")]
    [InlineData(MacRenderer)]
    [InlineData("/Applications/Claude.app/Contents/Frameworks/Claude Helper (GPU).app/Contents/MacOS/Claude Helper (GPU)")]
    [InlineData(MacCrashpad)]
    public void Mac_bundle_paths_are_desktop_processes(string exePath)
    {
        Mac.IsDesktopProcess(Process(exePath)).ShouldBeTrue();
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("/Users/USER/.local/bin/claude")]
    [InlineData("/Users/USER/Library/Application Support/Claude/claude-code/2.1.280/claude")]
    [InlineData("/Applications/Claude.app/Contents/Frameworks/claude-code/claude")]
    [InlineData("/Applications/Claude.app/Contents/Resources/app.asar.unpacked/claude")]
    [InlineData("/Applications/Claude Code.app/Contents/MacOS/Claude")]
    [InlineData(MsixExe)]
    [InlineData(null)]
    public void Other_mac_paths_are_not_desktop_processes(string? exePath)
    {
        Mac.IsDesktopProcess(Process(exePath)).ShouldBeFalse();
    }

    [Theory]
    [InlineData(MsixExe)]
    [InlineData(MacMain)]
    public void Linux_knows_no_desktop_process(string exePath)
    {
        DesktopProcessRules.For(OsKind.Linux, ProviderDescriptors.Claude).IsDesktopProcess(Process(exePath)).ShouldBeFalse();
    }

    [Theory]
    [InlineData(MsixExe, $"\"{MsixExe}\"")]
    [InlineData(MsixExe, $"\"{MsixExe}\" --user-data-dir=C:\\Users\\USER\\.mcacc\\profiles\\privat\\desktop")]
    [InlineData(SquirrelExe, $"\"{SquirrelExe}\"")]
    public void Windows_main_is_a_desktop_process_without_type(string exePath, string commandLine)
    {
        Windows.IsMain(new ProcessInfo(100, 1, exePath, commandLine, StartTime: null)).ShouldBeTrue();
    }

    [Theory]
    [InlineData(MsixExe, $"\"{MsixExe}\" --type=renderer --user-data-dir=\"C:\\Users\\USER\\AppData\\Roaming\\Claude\"")]
    [InlineData(MsixExe, $"\"{MsixExe}\" --type=crashpad-handler --user-data-dir=C:\\Users\\USER\\AppData\\Roaming\\Claude /prefetch:4")]
    [InlineData(@"C:\Users\USER\AppData\Roaming\Claude\claude-code\2.1.280\claude.exe", "claude")]
    [InlineData(MsixExe, null)]
    public void Windows_children_cli_processes_and_unreadable_command_lines_are_not_main(string exePath, string? commandLine)
    {
        Windows.IsMain(new ProcessInfo(100, 1, exePath, commandLine, StartTime: null)).ShouldBeFalse();
    }

    [Theory]
    [InlineData(MacMain, MacMain)]
    [InlineData(MacMain, MacMain + " --user-data-dir=/Users/USER/desk top ä")]
    public void Mac_main_is_the_bundle_exe_without_type(string exePath, string commandLine)
    {
        Mac.IsMain(new ProcessInfo(100, 1, exePath, commandLine, StartTime: null)).ShouldBeTrue();
    }

    [Theory]
    [InlineData(MacRenderer, MacRenderer + " --type=renderer --user-data-dir=/Users/USER/desk top ä")]
    [InlineData(MacCrashpad, MacCrashpad + " --no-rate-limit --monitor-self-annotation=ptype=crashpad-handler")]
    [InlineData(MacMain, MacMain + " --type=gpu-process")]
    public void Mac_helpers_are_not_main_even_without_type(string exePath, string commandLine)
    {
        Mac.IsMain(new ProcessInfo(100, 1, exePath, commandLine, StartTime: null)).ShouldBeFalse();
    }

    private static DesktopProcessRules CodexWindows => DesktopProcessRules.For(OsKind.Windows, ProviderDescriptors.Codex);

    private static ProcessInfo Exe(string path, string? commandLine = null) =>
        new(1, 0, path, commandLine ?? $"\"{path}\"", DateTimeOffset.Now);

    private const string CodexApp =
        @"C:\Program Files\WindowsApps\OpenAI.Codex_26.930.3930.0_x64__2p2nqsd0c76g0\app\ChatGPT.exe";

    [Fact]
    public void The_Codex_app_is_recognised_by_its_package_path()
    {
        CodexWindows.IsDesktopProcess(Exe(CodexApp)).ShouldBeTrue();
    }

    /// <summary>
    /// The separate ChatGPT app carries the same executable name. Matching on the name alone would make MCACC
    /// quit someone's chat window while switching a Codex account.
    /// </summary>
    [Fact]
    public void The_separate_ChatGPT_app_is_not_the_Codex_app()
    {
        const string other = @"C:\Program Files\WindowsApps\OpenAI.ChatGPT_1.2.3_x64__abcdefghijklm\app\ChatGPT.exe";

        CodexWindows.IsDesktopProcess(Exe(other)).ShouldBeFalse();
    }

    [Fact]
    public void Claude_rules_do_not_match_the_Codex_app_and_the_other_way_round()
    {
        const string claudeApp = @"C:\Program Files\WindowsApps\Claude_pzs8sxrjxfjjc\app\Claude.exe";

        DesktopProcessRules.For(OsKind.Windows, ProviderDescriptors.Claude).IsDesktopProcess(Exe(CodexApp)).ShouldBeFalse();
        CodexWindows.IsDesktopProcess(Exe(claudeApp)).ShouldBeFalse();
    }

    /// <summary>A render process carries --type=; only the one without it is the instance's main process.</summary>
    [Fact]
    public void Only_the_Codex_process_without_a_type_switch_is_the_main_one()
    {
        CodexWindows.IsMain(Exe(CodexApp)).ShouldBeTrue();
        CodexWindows.IsMain(Exe(CodexApp, $"\"{CodexApp}\" --type=renderer")).ShouldBeFalse();
    }
}
