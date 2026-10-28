using System.Runtime.Versioning;
using Mcacc.Core.Accounts;
using Mcacc.Core.ConfigCopy;
using Mcacc.Core.Model;
using Mcacc.Core.Paths;
using Mcacc.Core.Tests.TestSupport;
using Shouldly;
using Xunit;

namespace Mcacc.Core.Tests.Accounts;

public sealed class AccountServiceTests : IDisposable
{
    private static readonly Account Privat = new("privat", "Privat", "#6A9BCC", IsDefault: false);
    private static readonly Account Firma = new("firma", "Firma", "#7D9B76", IsDefault: false);

    private readonly TempDirectory _temp = new();
    private readonly McaccPaths _paths;
    private readonly AccountPathResolver _resolver;
    private readonly string _defaultClaudeDir;

    public AccountServiceTests()
    {
        var platform = PlatformInfo.Current() with
        {
            HomeDir = Path.Join(_temp.Path, "home"),
            RoamingAppData = Path.Join(_temp.Path, "roaming"),
            LocalAppData = Path.Join(_temp.Path, "local"),
        };
        _paths = new McaccPaths(Path.Join(_temp.Path, "mcacc"), platform.PathStyle);
        _resolver = new AccountPathResolver(_paths, platform);
        _defaultClaudeDir = Path.Join(platform.HomeDir, ".claude");
    }

    public void Dispose() => _temp.Dispose();

    private static FakeConfigStore Store(params Account[] others) => Store(new AppSettings(), others);

    private static FakeConfigStore Store(AppSettings settings, params Account[] others) =>
        new(ConfigWith(settings, others));

    private static FakeConfigStore ReadOnlyStore(params Account[] others) =>
        new(ConfigWith(new AppSettings(), others), isReadOnly: true);

    private static AppConfig ConfigWith(AppSettings settings, Account[] others) =>
        new(AppConfig.CurrentVersion, [Account.CreateDefault(), .. others], settings);

    private AccountService Service(FakeConfigStore store) =>
        new(store, _paths, _resolver, new ConfigCopier(), new ProfileDataDeleter());

    private static void WriteFile(string path, string content = "content")
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private static CopyRequest CopyFrom(string sourceId, bool rewrite, params CopyItem[] items) =>
        new(sourceId, items.ToHashSet(), rewrite);

    [Fact]
    public void Config_is_loaded_once_on_construction()
    {
        var store = Store(Privat);

        var service = Service(store);

        store.LoadCount.ShouldBe(1);
        service.LoadResult.ShouldBeSameAs(store.LoadResult);
        service.Config.ShouldBeSameAs(store.LoadResult.Config);
        store.Saved.ShouldBeEmpty();
    }

    [Fact]
    public void Create_adds_a_profile_account_with_its_directories_and_saves()
    {
        var store = Store(Privat);
        var service = Service(store);

        var result = service.Create("  Kunde Müller  ", "#7D9B76", copy: null);

        var created = new Account("kunde-mueller", "Kunde Müller", "#7D9B76", IsDefault: false);
        result.ShouldBe(new CreateAccountResult(created, CopyReport: null));
        Directory.Exists(_paths.DesktopDataDir("kunde-mueller", ProviderDescriptors.Claude)).ShouldBeTrue();
        Directory.Exists(_paths.ConfigDir("kunde-mueller", ProviderDescriptors.Claude)).ShouldBeTrue();
        store.Saved.Single().Accounts.ShouldBe([Account.CreateDefault(), Privat, created]);
        service.Config.ShouldBeSameAs(store.Saved.Single());
    }

    [Theory]
    [InlineData("  ", NameValidationError.Empty)]
    [InlineData("12345678901234567890123456789012345678901", NameValidationError.TooLong)]
    [InlineData(" PRIVAT ", NameValidationError.Duplicate)]
    public void Create_rejects_an_invalid_name_before_touching_the_disk(string name, NameValidationError error)
    {
        var store = Store(Privat);
        var service = Service(store);

        var exception = Should.Throw<AccountValidationException>(() => service.Create(name, "#7D9B76", copy: null));

        exception.Error.ShouldBe(error);
        store.Saved.ShouldBeEmpty();
        Directory.Exists(_paths.ProfilesDir).ShouldBeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("red")]
    [InlineData("#12345")]
    public void Create_rejects_an_invalid_color_before_touching_the_disk(string color)
    {
        var store = Store();
        var service = Service(store);

        Should.Throw<ArgumentException>(() => service.Create("Privat", color, copy: null));

        store.Saved.ShouldBeEmpty();
        Directory.Exists(_paths.ProfilesDir).ShouldBeFalse();
    }

    [Fact]
    public void Create_skips_slugs_of_existing_accounts()
    {
        var service = Service(Store(Privat));

        service.Create("Privat!", "#7D9B76", copy: null).Account.Id.ShouldBe("privat-2");
    }

    [Fact]
    public void Create_skips_slugs_whose_profile_directory_already_exists()
    {
        WriteFile(Path.Join(_paths.ConfigDir("firma", ProviderDescriptors.Claude), "CLAUDE.md"), "orphan");
        var service = Service(Store());

        service.Create("Firma", "#7D9B76", copy: null).Account.Id.ShouldBe("firma-2");
        File.ReadAllText(Path.Join(_paths.ConfigDir("firma", ProviderDescriptors.Claude), "CLAUDE.md")).ShouldBe("orphan");
    }

    [Fact]
    public void Create_skips_slugs_occupied_by_a_file()
    {
        WriteFile(_paths.ProfileDir("firma"), "not a directory");
        var service = Service(Store());

        service.Create("Firma", "#7D9B76", copy: null).Account.Id.ShouldBe("firma-2");
        File.ReadAllText(_paths.ProfileDir("firma")).ShouldBe("not a directory");
    }

    [Fact]
    public void Create_copies_and_rewrites_config_of_the_default_account()
    {
        WriteFile(Path.Join(_defaultClaudeDir, "CLAUDE.md"), "# memory");
        WriteFile(Path.Join(_defaultClaudeDir, "settings.json"), """{ "statusLine": "bash ~/.claude/status.sh" }""");
        WriteFile(Path.Join(_defaultClaudeDir, ".credentials.json"), "secret");
        var store = Store();
        var service = Service(store);

        var result = service.Create(
            "Privat", "#6A9BCC", CopyFrom(Account.DefaultId, true, CopyItem.ClaudeMd, CopyItem.SettingsJson, CopyItem.Skills));

        result.CopyReport.ShouldNotBeNull().Copied.ShouldBe([CopyItem.ClaudeMd, CopyItem.SettingsJson]);
        var claude = _paths.ConfigDir("privat", ProviderDescriptors.Claude);
        File.ReadAllText(Path.Join(claude, "CLAUDE.md")).ShouldBe("# memory");
        File.ReadAllText(Path.Join(claude, "settings.json"))
            .ShouldBe($$"""{ "statusLine": "bash {{claude.Replace('\\', '/')}}/status.sh" }""");
        File.Exists(Path.Join(claude, ".credentials.json")).ShouldBeFalse();
        store.Saved.Single().Accounts.Select(a => a.Id).ShouldBe([Account.DefaultId, "privat"]);
    }

    [Fact]
    public void Create_copies_config_of_another_profile()
    {
        WriteFile(Path.Join(_paths.ConfigDir("firma", ProviderDescriptors.Claude), "skills", "a", "SKILL.md"), "skill");
        WriteFile(Path.Join(_paths.DesktopDataDir("firma", ProviderDescriptors.Claude), "claude_desktop_config.json"), "{}");
        var service = Service(Store(Firma));

        var result = service.Create(
            "Privat", "#6A9BCC", CopyFrom("firma", false, CopyItem.Skills, CopyItem.DesktopMcpConfig));

        result.CopyReport.ShouldNotBeNull().Copied.ShouldBe([CopyItem.Skills, CopyItem.DesktopMcpConfig]);
        File.ReadAllText(Path.Join(_paths.ConfigDir("privat", ProviderDescriptors.Claude), "skills", "a", "SKILL.md")).ShouldBe("skill");
        File.Exists(Path.Join(_paths.DesktopDataDir("privat", ProviderDescriptors.Claude), "claude_desktop_config.json")).ShouldBeTrue();
    }

    [Fact]
    public void Create_with_an_unknown_copy_source_throws_before_touching_the_disk()
    {
        var store = Store();
        var service = Service(store);

        Should.Throw<ArgumentException>(() =>
            service.Create("Privat", "#6A9BCC", CopyFrom("missing", false, CopyItem.ClaudeMd)));

        store.Saved.ShouldBeEmpty();
        Directory.Exists(_paths.ProfilesDir).ShouldBeFalse();
    }

    [Fact]
    public void Create_removes_the_new_profile_when_saving_fails()
    {
        var store = Store(Privat);
        store.OnSave = _ => throw new IOException("disk full");
        var service = Service(store);

        Should.Throw<IOException>(() => service.Create("Firma", "#7D9B76", copy: null)).Message.ShouldBe("disk full");

        Path.Exists(_paths.ProfileDir("firma")).ShouldBeFalse();
        Directory.Exists(_paths.ProfileDir("privat")).ShouldBeFalse();
        service.Config.ShouldBeSameAs(store.LoadResult.Config);
    }

    [Fact]
    public void Create_removes_the_new_profile_when_copying_fails()
    {
        var source = Path.Join(_defaultClaudeDir, "CLAUDE.md");
        WriteFile(source);
        var store = Store();
        var service = Service(store);

        Exception? failure;
        using (Unreadable(source))
        {
            failure = Record.Exception(() =>
                service.Create("Privat", "#6A9BCC", CopyFrom(Account.DefaultId, false, CopyItem.ClaudeMd)));
        }

        (failure is IOException or UnauthorizedAccessException).ShouldBeTrue(failure?.ToString());
        Path.Exists(_paths.ProfileDir("privat")).ShouldBeFalse();
        store.Saved.ShouldBeEmpty();
    }

    [Fact]
    public void Create_rethrows_the_original_failure_when_the_rollback_fails_too()
    {
        var store = Store();
        FileStream? lockedInsideProfile = null;
        store.OnSave = _ =>
        {
            // An open handle blocks deleting the profile on Windows; elsewhere the rollback just succeeds.
            lockedInsideProfile = new FileStream(
                Path.Join(_paths.ConfigDir("privat", ProviderDescriptors.Claude), "busy.log"), FileMode.CreateNew, FileAccess.Write, FileShare.None);
            throw new IOException("disk full");
        };
        var service = Service(store);

        try
        {
            Should.Throw<IOException>(() => service.Create("Privat", "#6A9BCC", copy: null)).Message.ShouldBe("disk full");
        }
        finally
        {
            lockedInsideProfile?.Dispose();
        }
    }

    private static IDisposable Unreadable(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        }

        var restore = new UnixModeRestore(path, File.GetUnixFileMode(path));
        File.SetUnixFileMode(path, UnixFileMode.None);
        return restore;
    }

    [UnsupportedOSPlatform("windows")]
    private sealed class UnixModeRestore(string path, UnixFileMode mode) : IDisposable
    {
        public void Dispose() => File.SetUnixFileMode(path, mode);
    }

    [Fact]
    public void Update_changes_name_and_color_but_keeps_id_and_position()
    {
        var store = Store(Privat, Firma);
        var service = Service(store);

        var updated = service.Update("privat", "  Privat Neu ", "#9B7BA8");

        updated.ShouldBe(new Account("privat", "Privat Neu", "#9B7BA8", IsDefault: false));
        store.Saved.Single().Accounts.ShouldBe([Account.CreateDefault(), updated, Firma]);
        service.Config.ShouldBeSameAs(store.Saved.Single());
        Directory.Exists(_paths.ProfilesDir).ShouldBeFalse();
    }

    [Fact]
    public void Update_accepts_the_own_name_in_another_case()
    {
        var service = Service(Store(Privat));

        service.Update("privat", "PRIVAT", "#6A9BCC").Name.ShouldBe("PRIVAT");
    }

    [Fact]
    public void Update_rejects_the_name_of_another_account()
    {
        var store = Store(Privat);
        var service = Service(store);

        Should.Throw<AccountValidationException>(() => service.Update("privat", "standard", "#6A9BCC"))
            .Error.ShouldBe(NameValidationError.Duplicate);
        store.Saved.ShouldBeEmpty();
    }

    [Fact]
    public void Update_rejects_an_invalid_color()
    {
        var store = Store(Privat);

        Should.Throw<ArgumentException>(() => Service(store).Update("privat", "Privat", "blue"));
        store.Saved.ShouldBeEmpty();
    }

    [Fact]
    public void Update_of_the_default_account_keeps_it_default()
    {
        var service = Service(Store());

        service.Update(Account.DefaultId, "Firma", "#5E8C8A")
            .ShouldBe(new Account(Account.DefaultId, "Firma", "#5E8C8A", IsDefault: true));
    }

    [Fact]
    public void Update_of_an_unknown_account_throws()
    {
        var store = Store();

        Should.Throw<InvalidOperationException>(() => Service(store).Update("missing", "Privat", "#6A9BCC"));
        store.Saved.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(Account.DefaultId)]
    [InlineData("missing")]
    public void Remove_refuses_the_default_and_unknown_accounts(string id)
    {
        var store = Store(Privat);

        Should.Throw<InvalidOperationException>(() => Service(store).Remove(id, deleteData: true));
        store.Saved.ShouldBeEmpty();
    }

    [Fact]
    public void Remove_without_data_keeps_the_profile_directory()
    {
        WriteFile(Path.Join(_paths.ConfigDir("privat", ProviderDescriptors.Claude), "CLAUDE.md"));
        var store = Store(Privat, Firma);
        var service = Service(store);

        service.Remove("privat", deleteData: false);

        store.Saved.Single().Accounts.ShouldBe([Account.CreateDefault(), Firma]);
        service.Config.ShouldBeSameAs(store.Saved.Single());
        File.Exists(Path.Join(_paths.ConfigDir("privat", ProviderDescriptors.Claude), "CLAUDE.md")).ShouldBeTrue();
    }

    [Fact]
    public void Remove_with_data_deletes_the_profile_directory_before_saving()
    {
        WriteFile(Path.Join(_paths.ConfigDir("privat", ProviderDescriptors.Claude), "CLAUDE.md"));
        var store = Store(Privat);
        bool? existedAtSave = null;
        store.OnSave = _ => existedAtSave = Directory.Exists(_paths.ProfileDir("privat"));

        Service(store).Remove("privat", deleteData: true);

        existedAtSave.ShouldBe(false);
        store.Saved.Single().Accounts.ShouldBe([Account.CreateDefault()]);
    }

    [Fact]
    public void Remove_keeps_the_account_when_deleting_its_data_fails()
    {
        var unsafeAccount = new Account("..", "Kaputt", "#6A9BCC", IsDefault: false);
        var store = Store(unsafeAccount);
        var service = Service(store);

        Should.Throw<InvalidOperationException>(() => service.Remove("..", deleteData: true));

        store.Saved.ShouldBeEmpty();
        service.Config.Accounts.ShouldContain(unsafeAccount);
    }

    [Fact]
    public void Remove_clears_the_last_account_when_it_pointed_at_the_removed_one()
    {
        var store = Store(new AppSettings(LastAccountId: "privat", LastTarget: LaunchTarget.Code), Privat);

        Service(store).Remove("privat", deleteData: false);

        store.Saved.Single().Settings.ShouldBe(new AppSettings(LastTarget: LaunchTarget.Code));
    }

    [Fact]
    public void Remove_keeps_the_last_account_when_it_pointed_elsewhere()
    {
        var settings = new AppSettings(LastAccountId: "firma");
        var store = Store(settings, Privat, Firma);

        Service(store).Remove("privat", deleteData: false);

        store.Saved.Single().Settings.ShouldBe(settings);
    }

    [Fact]
    public void UpdateSettings_saves_the_changed_settings()
    {
        var store = Store(Privat);
        var service = Service(store);

        service.UpdateSettings(s => s with { CloseAfterLaunch = false, LastAccountId = "privat" });

        var saved = store.Saved.Single();
        saved.Settings.ShouldBe(new AppSettings(CloseAfterLaunch: false, LastAccountId: "privat"));
        saved.Accounts.ShouldBe([Account.CreateDefault(), Privat]);
        service.Config.ShouldBeSameAs(saved);
    }

    [Fact]
    public void Read_only_config_refuses_every_change_before_touching_the_disk()
    {
        WriteFile(Path.Join(_paths.ConfigDir("privat", ProviderDescriptors.Claude), "CLAUDE.md"));
        var store = ReadOnlyStore(Privat);
        var service = Service(store);

        Should.Throw<InvalidOperationException>(() => service.Create("Firma", "#7D9B76", copy: null));
        Should.Throw<InvalidOperationException>(() => service.Update("privat", "Privat Neu", "#7D9B76"));
        Should.Throw<InvalidOperationException>(() => service.Remove("privat", deleteData: true));
        Should.Throw<InvalidOperationException>(() => service.UpdateSettings(s => s with { CloseAfterLaunch = false }));

        store.Saved.ShouldBeEmpty();
        service.Config.ShouldBeSameAs(service.LoadResult.Config);
        Directory.GetDirectories(_paths.ProfilesDir).Select(Path.GetFileName).ShouldBe(["privat"]);
        File.Exists(Path.Join(_paths.ConfigDir("privat", ProviderDescriptors.Claude), "CLAUDE.md")).ShouldBeTrue();
    }

    [Fact]
    public void Creating_a_Codex_account_makes_only_the_Codex_folders()
    {
        var service = Service(Store());

        var result = service.Create("Codex Privat", "#5E8C8A", copy: null, ProviderKind.Codex);

        result.Account.Provider.ShouldBe(ProviderKind.Codex);
        Directory.Exists(_paths.ConfigDir(result.Account.Id, ProviderDescriptors.Codex)).ShouldBeTrue();
        Directory.Exists(_paths.DesktopDataDir(result.Account.Id, ProviderDescriptors.Codex)).ShouldBeTrue();
        Directory.Exists(_paths.ConfigDir(result.Account.Id, ProviderDescriptors.Claude)).ShouldBeFalse();
        Directory.Exists(_paths.DesktopDataDir(result.Account.Id, ProviderDescriptors.Claude)).ShouldBeFalse();
    }

    /// <summary>
    /// Copying across providers would write one provider's files into another's home. The editor filters its
    /// sources, but the service must refuse it on its own.
    /// </summary>
    [Fact]
    public void Refuses_a_copy_source_of_another_provider()
    {
        var service = Service(Store());

        var exception = Should.Throw<ArgumentException>(
            () => service.Create("Codex", "#5E8C8A", CopyFrom(Account.DefaultId, false, CopyItem.ClaudeMd), ProviderKind.Codex));

        exception.Message.ShouldContain("Claude");
        service.Config.Accounts.ShouldAllBe(a => a.Provider == ProviderKind.Claude);
    }
}
