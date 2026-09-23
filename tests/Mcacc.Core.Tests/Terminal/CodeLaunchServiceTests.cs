using Mcacc.Core.Abstractions;
using Mcacc.Core.Model;
using Mcacc.Core.Paths;
using Mcacc.Core.Processes;
using Mcacc.Core.Terminal;
using Mcacc.Core.Tests.TestSupport;
using Shouldly;
using Xunit;

namespace Mcacc.Core.Tests.Terminal;

public sealed class CodeLaunchServiceTests : IDisposable
{
    private static readonly Account Standard = Account.CreateDefault();
    private static readonly Account Privat = new("privat", "Privat", "#6A9BCC", IsDefault: false);

    private static readonly TerminalOption WindowsTerminal =
        new(TerminalIds.WindowsTerminal, "Windows Terminal", @"C:\Program Files\WindowsApps\wt.exe");
    private static readonly TerminalOption Pwsh = new(TerminalIds.Pwsh, "PowerShell 7", @"C:\Program Files\PowerShell\7\pwsh.exe");
    private static readonly TerminalOption WindowsPowerShell = new(
        TerminalIds.WindowsPowerShell,
        "Windows PowerShell",
        @"C:\WINDOWS\System32\WindowsPowerShell\v1.0\powershell.exe");
    private static readonly TerminalOption Cmd = new(TerminalIds.Cmd, "Eingabeaufforderung", @"C:\WINDOWS\system32\cmd.exe");
    private static readonly TerminalOption TerminalApp =
        new(TerminalIds.TerminalApp, "Terminal", "/System/Applications/Utilities/Terminal.app");

    private readonly TempDirectory _temp = new();
    private readonly FakeProcessRunner _runner = new();
    private readonly FakeTerminalCatalog _catalog = new();
    private readonly FakeExecutableLocator _locator = new();

    public void Dispose() => _temp.Dispose();

    private string Home => Path.Join(_temp.Path, "home");

    private PlatformInfo Platform(OsKind os) => new(os, Home, Path.Join(Home, "AppData", "Roaming"), Path.Join(Home, "AppData", "Local"));

    private McaccPaths Paths(PlatformInfo platform) => new(Path.Join(_temp.Path, ".mcacc"), platform.PathStyle);

    private CodeLaunchService Service(OsKind os = OsKind.Windows)
    {
        var platform = Platform(os);
        var paths = Paths(platform);
        return new CodeLaunchService(
            _catalog,
            _locator,
            _runner,
            new LaunchFileWriter(),
            new AccountPathResolver(paths, platform),
            paths,
            platform);
    }

    private ClaudeCodeLaunchRequest Request(Account account, OsKind os)
    {
        var platform = Platform(os);
        var paths = Paths(platform);
        return new ClaudeCodeLaunchRequest(
            account.Id,
            account.Name,
            new AccountPathResolver(paths, platform).Resolve(account).ClaudeConfigDir,
            platform.HomeDir,
            paths.LaunchDir,
            account.Color);
    }

    [Fact]
    public void Launch_writes_the_batch_file_and_starts_the_planned_process()
    {
        _catalog.Installed = [Cmd];

        var result = Service().Launch(Privat, new AppSettings());

        result.Outcome.ShouldBe(CodeLaunchOutcome.Launched);
        result.Terminal.ShouldBe(Cmd);
        var expected = WindowsTerminalPlanner.Plan(Request(Privat, OsKind.Windows), Cmd, WindowsTerminalShell.Auto, _catalog.Installed);
        _runner.Started.ShouldHaveSingleItem().ShouldBeEquivalentTo(expected.Process);
        var file = expected.Files.ShouldHaveSingleItem();
        File.ReadAllText(file.Path).ShouldBe(file.Content.Replace("\r\n", "\n").Replace("\n", "\r\n"));
    }

    [Fact]
    public void Launch_uses_the_terminal_from_the_settings()
    {
        _catalog.Installed = [WindowsTerminal, WindowsPowerShell, Cmd];

        var result = Service().Launch(Privat, new AppSettings(Terminal: TerminalIds.WindowsPowerShell));

        result.Terminal.ShouldBe(WindowsPowerShell);
        _runner.Started.ShouldHaveSingleItem().FileName.ShouldBe(WindowsPowerShell.Path);
    }

    [Fact]
    public void Launch_falls_back_to_the_platform_default_terminal()
    {
        _catalog.Installed = [WindowsPowerShell, WindowsTerminal, Cmd];
        _catalog.PlatformDefaultId = TerminalIds.WindowsTerminal;

        var result = Service().Launch(Privat, new AppSettings(Terminal: "ghostty"));

        result.Terminal.ShouldBe(WindowsTerminal);
    }

    [Fact]
    public void Launch_in_windows_terminal_with_powershell_writes_no_launch_file()
    {
        _catalog.Installed = [WindowsTerminal, Pwsh, WindowsPowerShell, Cmd];

        var result = Service().Launch(Privat, new AppSettings(Terminal: TerminalIds.WindowsTerminal));

        result.Outcome.ShouldBe(CodeLaunchOutcome.Launched);
        _runner.Started.ShouldHaveSingleItem().Arguments.ShouldContain(Pwsh.Path);
        Directory.Exists(Paths(Platform(OsKind.Windows)).LaunchDir).ShouldBeFalse();
    }

    [Fact]
    public void Launch_of_the_default_account_removes_the_variable_in_the_script()
    {
        _catalog.Installed = [WindowsPowerShell];

        Service().Launch(Standard, new AppSettings());

        var script = _runner.Started.ShouldHaveSingleItem().Arguments[^1];
        script.ShouldContain("Remove-Item Env:CLAUDE_CONFIG_DIR -ErrorAction Ignore");
    }

    [Fact]
    public void Launch_of_a_profile_account_sets_its_own_config_dir()
    {
        _catalog.Installed = [WindowsPowerShell];

        Service().Launch(Privat, new AppSettings());

        var script = _runner.Started.ShouldHaveSingleItem().Arguments[^1];
        script.ShouldContain(Paths(Platform(OsKind.Windows)).ClaudeConfigDir("privat"));
    }

    [Fact]
    public void Launch_passes_the_account_color_as_the_tab_color()
    {
        _catalog.Installed = [WindowsTerminal, WindowsPowerShell];

        Service().Launch(Privat, new AppSettings());

        var arguments = _runner.Started.ShouldHaveSingleItem().Arguments.ToList();
        arguments[arguments.IndexOf("--tabColor") + 1].ShouldBe(Privat.Color);
    }

    [Fact]
    public void Launch_writes_the_executable_command_file_and_opens_it_on_macos()
    {
        _catalog.Installed = [TerminalApp];
        _catalog.PlatformDefaultId = TerminalIds.TerminalApp;

        var result = Service(OsKind.MacOS).Launch(Privat, new AppSettings());

        result.Outcome.ShouldBe(CodeLaunchOutcome.Launched);
        var expected = MacTerminalPlanner.Plan(Request(Privat, OsKind.MacOS), TerminalApp);
        _runner.Started.ShouldHaveSingleItem().ShouldBeEquivalentTo(expected.Process);
        var file = expected.Files.ShouldHaveSingleItem();
        File.ReadAllText(file.Path).ShouldBe(file.Content);
    }

    [Fact]
    public void Launch_reports_no_terminal_when_none_is_installed()
    {
        var result = Service().Launch(Privat, new AppSettings());

        result.Outcome.ShouldBe(CodeLaunchOutcome.NoTerminal);
        result.Terminal.ShouldBeNull();
        _runner.Started.ShouldBeEmpty();
    }

    [Fact]
    public void Launch_reports_no_terminal_on_linux()
    {
        _catalog.Installed = [TerminalApp];

        var result = Service(OsKind.Linux).Launch(Privat, new AppSettings());

        result.Outcome.ShouldBe(CodeLaunchOutcome.NoTerminal);
        _runner.Started.ShouldBeEmpty();
    }

    [Fact]
    public void Launch_flags_a_missing_claude_cli_but_starts_anyway()
    {
        _catalog.Installed = [WindowsPowerShell];

        var result = Service().Launch(Privat, new AppSettings());

        result.Outcome.ShouldBe(CodeLaunchOutcome.Launched);
        result.ClaudeCliNotFound.ShouldBeTrue();
        _runner.Started.Count.ShouldBe(1);
    }

    [Fact]
    public void Launch_does_not_flag_a_claude_cli_that_is_on_the_path()
    {
        _catalog.Installed = [WindowsPowerShell];
        _locator.Found["claude"] = @"C:\Users\USER\AppData\Local\Microsoft\WinGet\Links\claude.exe";

        Service().Launch(Privat, new AppSettings()).ClaudeCliNotFound.ShouldBeFalse();
    }

    [Fact]
    public void Launch_reports_start_failed_when_the_runner_throws()
    {
        _catalog.Installed = [Cmd];
        _runner.OnStart = _ => throw new LaunchException("cmd.exe did not start.");

        var result = Service().Launch(Privat, new AppSettings());

        result.Outcome.ShouldBe(CodeLaunchOutcome.StartFailed);
        result.Terminal.ShouldBe(Cmd);
    }

    [Fact]
    public void Launch_reports_start_failed_when_the_configured_shell_is_no_longer_installed()
    {
        _catalog.Installed = [WindowsTerminal];

        var result = Service().Launch(Privat, new AppSettings(WindowsTerminalShell: WindowsTerminalShell.Pwsh));

        result.Outcome.ShouldBe(CodeLaunchOutcome.StartFailed);
        _runner.Started.ShouldBeEmpty();
    }

    private sealed class FakeTerminalCatalog : ITerminalCatalog
    {
        public IReadOnlyList<TerminalOption> Installed { get; set; } = [];

        public string PlatformDefaultId { get; set; } = TerminalIds.WindowsTerminal;

        public IReadOnlyList<TerminalOption> GetInstalled() => Installed;
    }

    private sealed class FakeExecutableLocator : IExecutableLocator
    {
        public Dictionary<string, string> Found { get; } = [];

        public string? Find(string name) => Found.GetValueOrDefault(name);
    }
}
