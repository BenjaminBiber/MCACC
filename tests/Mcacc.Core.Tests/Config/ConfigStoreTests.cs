using Mcacc.Core.Config;
using Mcacc.Core.Model;
using Mcacc.Core.Paths;
using Mcacc.Core.Tests.TestSupport;
using Shouldly;
using Xunit;

namespace Mcacc.Core.Tests.Config;

public sealed class ConfigStoreTests : IDisposable
{
    private const string QuarantineName = "config.json.corrupt-20260923140509";

    private static readonly Account Privat = new("privat", "Privat", "#6A9BCC", IsDefault: false);

    private const string ValidJson = """
        {
          "version": 1,
          "accounts": [
            { "id": "standard", "name": "Standard", "color": "#D97757", "isDefault": true },
            { "id": "privat", "name": "Privat", "color": "#6A9BCC", "isDefault": false }
          ],
          "settings": { "lastAccountId": "privat", "lastTarget": "code" }
        }
        """;

    private readonly TempDirectory _temp = new();
    private readonly McaccPaths _paths;

    // 12:05:09 UTC is 14:05:09 local in a UTC+2 zone.
    private readonly FakeTimeProvider _time = new(
        new DateTimeOffset(2026, 9, 23, 12, 5, 9, TimeSpan.Zero),
        TimeZoneInfo.CreateCustomTimeZone("mcacc-test", TimeSpan.FromHours(2), "mcacc-test", "mcacc-test"));

    public ConfigStoreTests()
    {
        _paths = new McaccPaths(Path.Combine(_temp.Path, "mcacc"), PlatformInfo.Current().PathStyle);
    }

    public void Dispose() => _temp.Dispose();

    private ConfigStore CreateStore() => new(_paths, _time);

    private void WriteFile(string path, string content)
    {
        Directory.CreateDirectory(_paths.Root);
        File.WriteAllText(path, content);
    }

    private string QuarantineFile => Path.Combine(_paths.Root, QuarantineName);

    [Fact]
    public void Missing_file_creates_and_saves_the_default_config()
    {
        var result = CreateStore().Load();

        result.Status.ShouldBe(ConfigLoadStatus.CreatedDefault);
        result.IsReadOnly.ShouldBeFalse();
        result.Repairs.ShouldBeEmpty();
        result.Config.Version.ShouldBe(AppConfig.CurrentVersion);
        result.Config.Accounts.ShouldBe([Account.CreateDefault()]);
        result.Config.Settings.ShouldBe(new AppSettings());
        File.Exists(_paths.ConfigFile).ShouldBeTrue();

        var reloaded = CreateStore().Load();
        reloaded.Status.ShouldBe(ConfigLoadStatus.Loaded);
        reloaded.Config.Accounts.ShouldBe([Account.CreateDefault()]);
    }

    [Fact]
    public void Existing_valid_file_is_loaded_without_writing()
    {
        WriteFile(_paths.ConfigFile, ValidJson);

        var result = CreateStore().Load();

        result.Status.ShouldBe(ConfigLoadStatus.Loaded);
        result.IsReadOnly.ShouldBeFalse();
        result.Repairs.ShouldBeEmpty();
        result.Config.Accounts.ShouldBe([Account.CreateDefault(), Privat]);
        result.Config.Settings.ShouldBe(new AppSettings(LastAccountId: "privat", LastTarget: LaunchTarget.Code));
        File.ReadAllText(_paths.ConfigFile).ShouldBe(ValidJson);
        File.Exists(_paths.ConfigBackupFile).ShouldBeFalse();
    }

    [Fact]
    public void Save_and_load_round_trip_keeps_unknown_top_level_fields()
    {
        WriteFile(_paths.ConfigFile, """
            { "version": 1, "accounts": [ { "id": "standard", "name": "Standard", "color": "#D97757", "isDefault": true } ],
              "settings": {}, "future": { "enabled": true } }
            """);
        var store = CreateStore();
        var loaded = store.Load().Config;

        store.Save(loaded with { Accounts = [.. loaded.Accounts, Privat], Settings = new AppSettings(CloseAfterLaunch: false) });

        var reloaded = CreateStore().Load();
        reloaded.Status.ShouldBe(ConfigLoadStatus.Loaded);
        reloaded.Config.Accounts.ShouldBe([Account.CreateDefault(), Privat]);
        reloaded.Config.Settings.ShouldBe(new AppSettings(CloseAfterLaunch: false));
        var extra = reloaded.Config.Extra.ShouldNotBeNull();
        extra.Keys.ShouldBe(["future"]);
        extra["future"].GetProperty("enabled").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public void Second_save_keeps_the_previous_file_as_backup()
    {
        var store = CreateStore();
        var first = DefaultConfigFactory.Create();
        var second = first with { Accounts = [.. first.Accounts, Privat] };

        store.Save(first);
        File.Exists(_paths.ConfigBackupFile).ShouldBeFalse();
        var firstText = File.ReadAllText(_paths.ConfigFile);

        store.Save(second);

        File.ReadAllText(_paths.ConfigBackupFile).ShouldBe(firstText);
        CreateStore().Load().Config.Accounts.ShouldBe([Account.CreateDefault(), Privat]);
        Directory.GetFiles(_paths.Root).Select(Path.GetFileName).Order()
            .ShouldBe(["config.json", "config.json.bak"]);
    }

    [Fact]
    public void Corrupt_file_is_quarantined_and_recovered_from_backup()
    {
        WriteFile(_paths.ConfigFile, "{ \"version\": 1, \"accounts\": [");
        WriteFile(_paths.ConfigBackupFile, ValidJson);

        var result = CreateStore().Load();

        result.Status.ShouldBe(ConfigLoadStatus.RecoveredFromBackup);
        result.IsReadOnly.ShouldBeFalse();
        result.Config.Accounts.ShouldBe([Account.CreateDefault(), Privat]);
        File.ReadAllText(QuarantineFile).ShouldBe("{ \"version\": 1, \"accounts\": [");
        var reloaded = CreateStore().Load();
        reloaded.Status.ShouldBe(ConfigLoadStatus.Loaded);
        reloaded.Config.Accounts.ShouldBe([Account.CreateDefault(), Privat]);
    }

    [Theory]
    [InlineData("this is not json")]
    [InlineData("null")]
    [InlineData("")]
    public void Corrupt_file_without_backup_is_quarantined_and_reset_to_default(string content)
    {
        WriteFile(_paths.ConfigFile, content);

        var result = CreateStore().Load();

        result.Status.ShouldBe(ConfigLoadStatus.ResetAfterCorruption);
        result.IsReadOnly.ShouldBeFalse();
        result.Config.Accounts.ShouldBe([Account.CreateDefault()]);
        File.ReadAllText(QuarantineFile).ShouldBe(content);
        CreateStore().Load().Status.ShouldBe(ConfigLoadStatus.Loaded);
    }

    [Fact]
    public void Corrupt_file_with_corrupt_backup_is_reset_to_default()
    {
        WriteFile(_paths.ConfigFile, "{");
        WriteFile(_paths.ConfigBackupFile, "[]");

        var result = CreateStore().Load();

        result.Status.ShouldBe(ConfigLoadStatus.ResetAfterCorruption);
        result.Config.Accounts.ShouldBe([Account.CreateDefault()]);
        File.Exists(QuarantineFile).ShouldBeTrue();
    }

    [Fact]
    public void Newer_version_is_loaded_read_only_and_save_throws()
    {
        const string newer = """
            { "version": 2, "accounts": [ { "id": "standard", "name": "Standard", "color": "#D97757", "isDefault": true } ],
              "settings": {}, "profilesV2": [] }
            """;
        WriteFile(_paths.ConfigFile, newer);
        var store = CreateStore();

        var result = store.Load();

        result.Status.ShouldBe(ConfigLoadStatus.NewerVersionReadOnly);
        result.IsReadOnly.ShouldBeTrue();
        result.Config.Version.ShouldBe(2);
        result.Config.Accounts.ShouldBe([Account.CreateDefault()]);
        Should.Throw<InvalidOperationException>(() => store.Save(result.Config));
        File.ReadAllText(_paths.ConfigFile).ShouldBe(newer);
        File.Exists(_paths.ConfigBackupFile).ShouldBeFalse();
    }

    [Fact]
    public void Repaired_config_is_saved()
    {
        WriteFile(_paths.ConfigFile, """
            { "version": 1, "accounts": [ { "id": "Bad Id", "name": "x", "color": "#D97757", "isDefault": false },
                                          { "id": "privat", "name": "Privat", "color": "#6A9BCC", "isDefault": false } ],
              "settings": {} }
            """);

        var result = CreateStore().Load();

        result.Status.ShouldBe(ConfigLoadStatus.Loaded);
        result.Repairs.ShouldNotBeEmpty();
        result.Config.Accounts.ShouldBe([Account.CreateDefault(), Privat]);
        var reloaded = CreateStore().Load();
        reloaded.Repairs.ShouldBeEmpty();
        reloaded.Config.Accounts.ShouldBe([Account.CreateDefault(), Privat]);
    }

    [Fact]
    public void Structurally_incomplete_file_is_repaired_instead_of_quarantined()
    {
        WriteFile(_paths.ConfigFile, """{ "version": 1 }""");

        var result = CreateStore().Load();

        result.Status.ShouldBe(ConfigLoadStatus.Loaded);
        result.Repairs.ShouldNotBeEmpty();
        result.Config.Accounts.ShouldBe([Account.CreateDefault()]);
        result.Config.Settings.ShouldBe(new AppSettings());
        File.Exists(QuarantineFile).ShouldBeFalse();
    }

    [Fact]
    public void Repairs_of_a_newer_version_are_not_saved()
    {
        const string newer = """{ "version": 2, "accounts": [], "settings": {} }""";
        WriteFile(_paths.ConfigFile, newer);

        var result = CreateStore().Load();

        result.IsReadOnly.ShouldBeTrue();
        result.Repairs.ShouldNotBeEmpty();
        result.Config.Accounts.ShouldBe([Account.CreateDefault()]);
        File.ReadAllText(_paths.ConfigFile).ShouldBe(newer);
    }

    [Fact]
    public void Newer_version_backup_recovered_after_corruption_stays_read_only()
    {
        WriteFile(_paths.ConfigFile, "{");
        WriteFile(_paths.ConfigBackupFile, """{ "version": 2, "accounts": [], "settings": {} }""");
        var store = CreateStore();

        var result = store.Load();

        result.Status.ShouldBe(ConfigLoadStatus.RecoveredFromBackup);
        result.IsReadOnly.ShouldBeTrue();
        File.Exists(_paths.ConfigFile).ShouldBeFalse();
        Should.Throw<InvalidOperationException>(() => store.Save(result.Config));
    }
}
