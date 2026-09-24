using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Mcacc.Core.FileSystem;

/// <summary>One json document on disk. A write goes to a temp file first, so a crash never leaves half a file.</summary>
public static class JsonFile
{
    /// <returns>Null when the file is missing, unreadable or not valid json for <paramref name="type"/>.</returns>
    public static T? Read<T>(string path, JsonTypeInfo<T> type)
        where T : class
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            return JsonSerializer.Deserialize(stream, type);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    public static void Write<T>(string path, T value, JsonTypeInfo<T> type)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tempFile = path + ".tmp";
        using (var stream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            JsonSerializer.Serialize(stream, value, type);
            stream.Flush(flushToDisk: true);
        }

        File.Move(tempFile, path, overwrite: true);
    }
}
