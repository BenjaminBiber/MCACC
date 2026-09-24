using Mcacc.Core.Config;
using Mcacc.Core.FileSystem;
using Mcacc.Core.Paths;

namespace Mcacc.Core.Stats;

public sealed class StatsIndexStore(McaccPaths paths)
{
    /// <summary>A missing, broken or outdated index loads as empty: the scanner then simply reads everything again.</summary>
    public StatsIndex Load(string accountId) =>
        JsonFile.Read(paths.StatsFile(accountId), McaccCompactJsonContext.Default.StatsIndex)
            is { Version: StatsIndex.CurrentVersion, Files: not null } index
            ? index
            : StatsIndex.Empty;

    public void Save(string accountId, StatsIndex index) =>
        JsonFile.Write(paths.StatsFile(accountId), index, McaccCompactJsonContext.Default.StatsIndex);

    public void Delete(string accountId)
    {
        var file = paths.StatsFile(accountId);
        if (File.Exists(file))
        {
            File.Delete(file);
        }
    }
}
