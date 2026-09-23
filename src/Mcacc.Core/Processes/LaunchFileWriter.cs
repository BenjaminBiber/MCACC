using System.Text;

namespace Mcacc.Core.Processes;

public sealed class LaunchFileWriter
{
    private const UnixFileMode OwnerOnlyExecutable = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;

    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);

    public void Write(LaunchFile file)
    {
        var directory = Path.GetDirectoryName(file.Path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(file.Path, Normalize(file.Content, file.LineEnding), Utf8WithoutBom);
        if (file.Executable && !OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(file.Path, OwnerOnlyExecutable);
        }
    }

    // Only CR and LF: string.ReplaceLineEndings would also turn U+2028, NEL and FF into real line breaks,
    // which would let a title or path start a new batch command.
    private static string Normalize(string content, LineEnding lineEnding)
    {
        var lf = content.Replace("\r\n", "\n").Replace('\r', '\n');
        return lineEnding == LineEnding.CrLf ? lf.Replace("\n", "\r\n") : lf;
    }
}
