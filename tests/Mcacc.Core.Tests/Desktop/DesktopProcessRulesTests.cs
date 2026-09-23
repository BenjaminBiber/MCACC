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

    private static readonly DesktopProcessRules Windows = DesktopProcessRules.ForOs(OsKind.Windows);
    private static readonly DesktopProcessRules Mac = DesktopProcessRules.ForOs(OsKind.MacOS);

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
        DesktopProcessRules.ForOs(OsKind.Linux).IsDesktopProcess(Process(exePath)).ShouldBeFalse();
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
}
