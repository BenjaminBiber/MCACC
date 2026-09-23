using System.Globalization;
using System.Text.Json;
using Mcacc.Core.Abstractions;
using Mcacc.Core.Model;
using Mcacc.Core.Paths;

namespace Mcacc.Core.Config;

public sealed class ConfigStore(McaccPaths paths, TimeProvider? time = null) : IConfigStore
{
    private readonly TimeProvider _time = time ?? TimeProvider.System;
    private bool _isReadOnly;

    public ConfigLoadResult Load()
    {
        _isReadOnly = false;
        if (!File.Exists(paths.ConfigFile))
        {
            var created = DefaultConfigFactory.Create();
            Save(created);
            return new ConfigLoadResult(created, ConfigLoadStatus.CreatedDefault, IsReadOnly: false, Repairs: []);
        }

        var (config, status) = ReadOrRecover();
        var isReadOnly = config.Version > AppConfig.CurrentVersion;
        if (isReadOnly && status == ConfigLoadStatus.Loaded)
        {
            status = ConfigLoadStatus.NewerVersionReadOnly;
        }

        var (repaired, repairs) = ConfigValidator.Repair(config);
        var mustPersist = status is ConfigLoadStatus.RecoveredFromBackup or ConfigLoadStatus.ResetAfterCorruption
            || repairs.Count > 0;
        if (mustPersist && !isReadOnly)
        {
            Save(repaired);
        }

        _isReadOnly = isReadOnly;
        return new ConfigLoadResult(repaired, status, isReadOnly, repairs);
    }

    public void Save(AppConfig config)
    {
        if (_isReadOnly)
        {
            throw new InvalidOperationException("The config was written by a newer MCACC version and is read-only.");
        }

        Directory.CreateDirectory(paths.Root);
        var tempFile = paths.ConfigFile + ".tmp";
        using (var stream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            JsonSerializer.Serialize(stream, config, McaccJsonContext.Default.AppConfig);
            stream.Flush(flushToDisk: true);
        }

        if (File.Exists(paths.ConfigFile))
        {
            File.Copy(paths.ConfigFile, paths.ConfigBackupFile, overwrite: true);
        }

        File.Move(tempFile, paths.ConfigFile, overwrite: true);
    }

    private (AppConfig Config, ConfigLoadStatus Status) ReadOrRecover()
    {
        if (Read(paths.ConfigFile) is { } config)
        {
            return (config, ConfigLoadStatus.Loaded);
        }

        Quarantine();
        return TryReadBackup() is { } backup
            ? (backup, ConfigLoadStatus.RecoveredFromBackup)
            : (DefaultConfigFactory.Create(), ConfigLoadStatus.ResetAfterCorruption);
    }

    private void Quarantine()
    {
        var stamp = _time.GetLocalNow().ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        File.Move(paths.ConfigFile, $"{paths.ConfigFile}.corrupt-{stamp}", overwrite: true);
    }

    private AppConfig? TryReadBackup()
    {
        try
        {
            return Read(paths.ConfigBackupFile);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <returns>Null when the file is not a JSON config object.</returns>
    private static AppConfig? Read(string file)
    {
        using var stream = File.OpenRead(file);
        try
        {
            return JsonSerializer.Deserialize(stream, McaccJsonContext.Default.AppConfig);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
