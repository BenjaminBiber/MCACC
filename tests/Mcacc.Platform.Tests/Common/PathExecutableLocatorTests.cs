using Mcacc.Core.Model;
using Mcacc.Core.Paths;
using Mcacc.Platform.Common;
using Mcacc.Platform.Tests.TestSupport;
using Shouldly;
using Xunit;

namespace Mcacc.Platform.Tests.Common;

[Trait("Category", "Integration")]
public sealed class PathExecutableLocatorTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    private static PathExecutableLocator Current() => new(PlatformInfo.Current());

    private static string ExecutableName(string name) => OperatingSystem.IsWindows() ? name + ".exe" : name;

    private string WriteExecutable(string directory, string name)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Join(directory, ExecutableName(name));
        File.WriteAllText(path, "");
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        return path;
    }

    [WindowsFact]
    public void Finds_cmd_on_windows()
    {
        var path = Current().Find("cmd");

        path.ShouldNotBeNull();
        // PATHEXT is upper case, so the extension comes back as the environment spells it.
        Path.GetFileName(path).ShouldBe("cmd.exe", StringCompareShould.IgnoreCase);
        File.Exists(path).ShouldBeTrue();
    }

    [WindowsFact]
    public void Finds_an_executable_that_already_carries_its_extension()
    {
        Current().Find("cmd.exe").ShouldNotBeNull();
    }

    [MacFact]
    public void Finds_sh_on_macos()
    {
        Current().Find("sh").ShouldNotBeNull();
    }

    [Fact]
    public void Reports_an_unknown_executable_as_missing()
    {
        Current().Find("mcacc-does-not-exist").ShouldBeNull();
    }

    [Fact]
    public void Finds_an_executable_in_a_path_entry()
    {
        var directory = Path.Join(_temp.Path, "bin");
        var expected = WriteExecutable(directory, "mcacc-probe");
        var locator = new PathExecutableLocator(PlatformInfo.Current(), name => name == "PATH" ? directory : null);

        locator.Find("mcacc-probe").ShouldBe(expected);
    }

    [Fact]
    public void Searches_the_extra_directories_even_with_an_empty_path()
    {
        var info = PlatformInfo.Current() with { HomeDir = _temp.Path, LocalAppData = _temp.Path };
        var directory = info.Os == OsKind.Windows
            ? info.PathStyle.Join(_temp.Path, "Microsoft", "WinGet", "Links")
            : info.PathStyle.Join(_temp.Path, ".local", "bin");
        var expected = WriteExecutable(directory, "mcacc-probe");
        var locator = new PathExecutableLocator(info, _ => "");

        locator.Find("mcacc-probe").ShouldBe(expected);
    }

    [Fact]
    public void Does_not_return_a_directory_that_carries_the_name()
    {
        var directory = Path.Join(_temp.Path, "bin");
        Directory.CreateDirectory(Path.Join(directory, ExecutableName("mcacc-probe")));
        var locator = new PathExecutableLocator(PlatformInfo.Current(), name => name == "PATH" ? directory : null);

        locator.Find("mcacc-probe").ShouldBeNull();
    }

    /// <summary>
    /// MCACC does not inherit a login shell's PATH, so the Codex install root has to be on the extra list the way
    /// <c>~/.claude/local</c> is for Claude.
    /// </summary>
    [WindowsFact]
    public void Finds_codex_in_its_windows_install_directory_without_PATH()
    {
        var localAppData = Path.Join(_temp.Path, "local");
        var info = PlatformInfo.Current() with
        {
            HomeDir = Path.Join(_temp.Path, "home"),
            LocalAppData = localAppData,
        };
        var expected = WriteExecutable(Path.Join(localAppData, "Programs", "OpenAI", "Codex", "bin"), "codex");

        new PathExecutableLocator(info, _ => null).Find("codex").ShouldBe(expected);
    }

    [Fact]
    public void Finds_codex_in_the_standalone_package_directory_without_PATH()
    {
        var home = Path.Join(_temp.Path, "home");
        var info = PlatformInfo.Current() with { HomeDir = home, LocalAppData = Path.Join(_temp.Path, "local") };
        var expected = WriteExecutable(
            Path.Join(home, ".codex", "packages", "standalone", "current", "bin"),
            "codex");

        new PathExecutableLocator(info, _ => null).Find("codex").ShouldBe(expected);
    }
}
