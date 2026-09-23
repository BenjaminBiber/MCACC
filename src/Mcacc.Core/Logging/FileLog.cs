using System.Globalization;
using System.Text;
using Mcacc.Core.Paths;

namespace Mcacc.Core.Logging;

/// <summary>
/// Appends one line per entry to <c>logs/mcacc-yyyyMMdd.log</c> and drops files older than a week. Writing never
/// throws: a broken log must not take the app down.
/// </summary>
public sealed class FileLog : ILog
{
    private const int KeepDays = 7;

    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly McaccPaths _paths;
    private readonly TimeProvider _time;
    private readonly Lock _gate = new();

    public FileLog(McaccPaths paths, TimeProvider? time = null)
    {
        _paths = paths;
        _time = time ?? TimeProvider.System;
        RemoveOldFiles();
    }

    public void Info(string message) => Write("INFO ", message, exception: null);

    public void Warn(string message, Exception? exception = null) => Write("WARN ", message, exception);

    public void Error(string message, Exception? exception = null) => Write("ERROR", message, exception);

    private void Write(string level, string message, Exception? exception)
    {
        var now = _time.GetLocalNow();
        var line = new StringBuilder()
            .Append(now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture))
            .Append(' ')
            .Append(level)
            .Append(' ')
            .Append(OneLine(message));
        if (exception is not null)
        {
            line.Append(" | ").Append(exception.GetType().FullName).Append(": ").Append(OneLine(exception.Message));
        }

        line.Append('\n');
        try
        {
            lock (_gate)
            {
                Directory.CreateDirectory(_paths.LogsDir);
                File.AppendAllText(FileFor(now), line.ToString(), Utf8WithoutBom);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // A log that cannot be written is not worth an error dialog.
        }
    }

    private static string OneLine(string text) => text.ReplaceLineEndings(" ");

    private string FileFor(DateTimeOffset when) =>
        _paths.Style.Join(_paths.LogsDir, $"mcacc-{when:yyyyMMdd}.log");

    private void RemoveOldFiles()
    {
        var oldest = _time.GetLocalNow().Date.AddDays(-KeepDays);
        try
        {
            foreach (var file in Directory.EnumerateFiles(_paths.LogsDir, "mcacc-*.log"))
            {
                if (DateOfFile(file) is { } date && date < oldest)
                {
                    File.Delete(file);
                }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or DirectoryNotFoundException)
        {
            // Nothing to clean up, or the directory is not ours to change.
        }
    }

    private static DateTime? DateOfFile(string file)
    {
        var name = Path.GetFileNameWithoutExtension(file);
        return name.Length == "mcacc-yyyyMMdd".Length
               && DateTime.TryParseExact(
                   name[6..],
                   "yyyyMMdd",
                   CultureInfo.InvariantCulture,
                   DateTimeStyles.None,
                   out var date)
            ? date
            : null;
    }
}
