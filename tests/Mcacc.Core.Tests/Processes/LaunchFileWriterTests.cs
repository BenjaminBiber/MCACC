using System.Text;
using Mcacc.Core.Processes;
using Mcacc.Core.Tests.TestSupport;
using Shouldly;
using Xunit;

namespace Mcacc.Core.Tests.Processes;

public sealed class LaunchFileWriterTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly LaunchFileWriter _writer = new();

    public void Dispose() => _temp.Dispose();

    private string LaunchPath(string name) => Path.Join(_temp.Path, "launch", name);

    [Fact]
    public void Creates_the_missing_directory_and_writes_the_file()
    {
        var path = LaunchPath("privat.cmd");

        _writer.Write(new LaunchFile(path, "claude", LineEnding.CrLf, Executable: false));

        File.ReadAllText(path).ShouldBe("claude");
    }

    [Fact]
    public void Normalizes_every_line_break_to_crlf()
    {
        var path = LaunchPath("privat.cmd");

        _writer.Write(new LaunchFile(path, "a\nb\r\nc\rd\r\n", LineEnding.CrLf, Executable: false));

        File.ReadAllBytes(path).ShouldBe(Encoding.ASCII.GetBytes("a\r\nb\r\nc\r\nd\r\n"));
    }

    [Fact]
    public void Normalizes_every_line_break_to_lf()
    {
        var path = LaunchPath("privat.command");

        _writer.Write(new LaunchFile(path, "a\r\nb\nc\rd\r\n", LineEnding.Lf, Executable: false));

        File.ReadAllBytes(path).ShouldBe(Encoding.ASCII.GetBytes("a\nb\nc\nd\n"));
    }

    [Fact]
    public void Keeps_unicode_line_separators_because_shells_do_not_break_lines_there()
    {
        var path = LaunchPath("privat.cmd");
        var content = $"title a{(char)0x2028}b{(char)0x0085}c{(char)0x000C}d";

        _writer.Write(new LaunchFile(path, content, LineEnding.CrLf, Executable: false));

        File.ReadAllText(path).ShouldBe(content);
    }

    [Fact]
    public void Writes_utf8_without_bom()
    {
        var path = LaunchPath("privat.cmd");

        _writer.Write(new LaunchFile(path, "Jö’s", LineEnding.CrLf, Executable: false));

        File.ReadAllBytes(path).ShouldBe([(byte)'J', 0xC3, 0xB6, 0xE2, 0x80, 0x99, (byte)'s']);
    }

    [Fact]
    public void Replaces_an_existing_longer_file()
    {
        var path = LaunchPath("privat.cmd");
        _writer.Write(new LaunchFile(path, "a much longer previous content", LineEnding.CrLf, Executable: false));

        _writer.Write(new LaunchFile(path, "short", LineEnding.CrLf, Executable: false));

        File.ReadAllText(path).ShouldBe("short");
    }

    [Fact]
    public void Executable_file_gets_mode_0700_and_other_files_stay_not_executable()
    {
        var executable = LaunchPath("privat.command");
        var plain = LaunchPath("other.command");

        _writer.Write(new LaunchFile(executable, "#!/bin/zsh\n", LineEnding.Lf, Executable: true));
        _writer.Write(new LaunchFile(plain, "#!/bin/zsh\n", LineEnding.Lf, Executable: false));

        File.ReadAllText(executable).ShouldBe("#!/bin/zsh\n");
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("Unix file modes do not exist on Windows.");
        }
        else
        {
            File.GetUnixFileMode(executable)
                .ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            var anyExecute = UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;
            (File.GetUnixFileMode(plain) & anyExecute).ShouldBe(UnixFileMode.None);
        }
    }
}
