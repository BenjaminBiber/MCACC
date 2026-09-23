using System.Text.Json;
using Mcacc.Core.Config;
using Mcacc.Core.Paths;

namespace Mcacc.Core.Usage;

/// <summary>The last known limits per account id. It holds no tokens, and a broken file is simply forgotten.</summary>
public sealed class UsageCache(McaccPaths paths)
{
    public IReadOnlyDictionary<string, UsageSnapshot> Load()
    {
        try
        {
            using var stream = File.OpenRead(paths.UsageCacheFile);
            return JsonSerializer.Deserialize(stream, McaccJsonContext.Default.DictionaryStringUsageSnapshot) ?? [];
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            return new Dictionary<string, UsageSnapshot>();
        }
    }

    public void Save(IReadOnlyDictionary<string, UsageSnapshot> snapshots)
    {
        Directory.CreateDirectory(paths.Root);
        var tempFile = paths.UsageCacheFile + ".tmp";
        var entries = snapshots.ToDictionary(entry => entry.Key, entry => entry.Value);
        using (var stream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            JsonSerializer.Serialize(stream, entries, McaccJsonContext.Default.DictionaryStringUsageSnapshot);
            stream.Flush(flushToDisk: true);
        }

        File.Move(tempFile, paths.UsageCacheFile, overwrite: true);
    }
}
