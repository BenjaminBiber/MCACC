using Mcacc.Core.Config;
using Mcacc.Core.FileSystem;
using Mcacc.Core.Paths;

namespace Mcacc.Core.Usage;

/// <summary>The last known limits per account id. It holds no tokens, and a broken file is simply forgotten.</summary>
public sealed class UsageCache(McaccPaths paths)
{
    public IReadOnlyDictionary<string, UsageSnapshot> Load() =>
        JsonFile.Read(paths.UsageCacheFile, McaccJsonContext.Default.DictionaryStringUsageSnapshot) ?? [];

    public void Save(IReadOnlyDictionary<string, UsageSnapshot> snapshots) =>
        JsonFile.Write(
            paths.UsageCacheFile,
            snapshots.ToDictionary(entry => entry.Key, entry => entry.Value),
            McaccJsonContext.Default.DictionaryStringUsageSnapshot);
}
