using Mcacc.Core.Abstractions;
using Mcacc.Core.Config;
using Mcacc.Core.Model;

namespace Mcacc.Core.Tests.TestSupport;

internal sealed class FakeConfigStore(AppConfig config, bool isReadOnly = false) : IConfigStore
{
    public ConfigLoadResult LoadResult { get; } = new(
        config,
        isReadOnly ? ConfigLoadStatus.NewerVersionReadOnly : ConfigLoadStatus.Loaded,
        isReadOnly,
        Repairs: []);

    public int LoadCount { get; private set; }

    public List<AppConfig> Saved { get; } = [];

    /// <summary>Runs before a save is recorded, to observe the disk at that moment.</summary>
    public Action<AppConfig>? OnSave { get; set; }

    public ConfigLoadResult Load()
    {
        LoadCount++;
        return LoadResult;
    }

    public void Save(AppConfig config)
    {
        OnSave?.Invoke(config);
        Saved.Add(config);
    }
}
