using Mcacc.Core.Config;
using Mcacc.Core.FileSystem;
using Mcacc.Core.Paths;

namespace Mcacc.Core.Usage;

/// <summary>The last profile per account, so the info page shows the account facts offline too. Holds no tokens.</summary>
public sealed class ProfileCache(McaccPaths paths)
{
    public IReadOnlyDictionary<string, AccountProfile> Load() =>
        JsonFile.Read(paths.ProfileCacheFile, McaccJsonContext.Default.DictionaryStringAccountProfile) ?? [];

    public void Save(IReadOnlyDictionary<string, AccountProfile> profiles) =>
        JsonFile.Write(
            paths.ProfileCacheFile,
            profiles.ToDictionary(entry => entry.Key, entry => entry.Value),
            McaccJsonContext.Default.DictionaryStringAccountProfile);
}
