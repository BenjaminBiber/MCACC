using Mcacc.Core.Model;
using Mcacc.Core.ConfigCopy;
using Mcacc.Core.Paths;
using Mcacc.Core.Tests.TestSupport;
using Shouldly;
using Xunit;

namespace Mcacc.Core.Tests.ConfigCopy;

public sealed class ConfigCopyPlannerTests : IDisposable
{
    private static readonly CopyItem[] ClaudeItems = [.. ProviderDescriptors.Claude.CopyItems];

    private readonly TempDirectory _temp = new();
    private readonly string _sourceClaude;
    private readonly string _msixDesktop;
    private readonly string _roamingDesktop;
    private readonly AccountPaths _source;
    private readonly AccountPaths _target;

    public ConfigCopyPlannerTests()
    {
        _sourceClaude = Path.Join(_temp.Path, "home", ".claude");
        _msixDesktop = Path.Join(_temp.Path, "msix", "Claude");
        _roamingDesktop = Path.Join(_temp.Path, "roaming", "Claude");
        _source = new AccountPaths(
            ProviderDescriptors.Claude,
            DesktopUserDataDir: null,
            ConfigDirOverride: null,
            EffectiveConfigDir: _sourceClaude,
            IdentityFile: Path.Join(_temp.Path, "home", ".claude.json"),
            CredentialsFile: Path.Join(_sourceClaude, ".credentials.json"),
            DesktopDataDirs: [_msixDesktop, _roamingDesktop]);
        _target = ProfilePaths("privat");
    }

    public void Dispose() => _temp.Dispose();

    private AccountPaths ProfilePaths(string id)
    {
        var profile = Path.Join(_temp.Path, "mcacc", "profiles", id);
        var claude = Path.Join(profile, "claude");
        var desktop = Path.Join(profile, "desktop");
        return new AccountPaths(
            ProviderDescriptors.Claude, desktop, claude, claude, Path.Join(claude, ".claude.json"), Path.Join(claude, ".credentials.json"), [desktop]);
    }

    private static void CreateFile(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{}");
    }

    private void CreateAllClaudeItems()
    {
        CreateFile(Path.Join(_sourceClaude, "CLAUDE.md"));
        CreateFile(Path.Join(_sourceClaude, "settings.json"));
        foreach (var dir in new[] { "agents", "skills", "commands", "scripts" })
        {
            Directory.CreateDirectory(Path.Join(_sourceClaude, dir));
        }
    }

    private static CopyRequest Request(bool rewrite, params CopyItem[] items) =>
        new("standard", items.ToHashSet(), rewrite);

    [Fact]
    public void Nothing_is_available_when_the_source_does_not_exist()
    {
        ConfigCopyPlanner.Available(_source).ShouldBeEmpty();
    }

    [Fact]
    public void Items_that_exist_on_disk_are_available()
    {
        CreateAllClaudeItems();
        CreateFile(Path.Join(_roamingDesktop, "claude_desktop_config.json"));

        ConfigCopyPlanner.Available(_source).ShouldBe(ClaudeItems, ignoreOrder: true);
    }

    [Fact]
    public void Item_of_the_wrong_kind_is_not_available()
    {
        Directory.CreateDirectory(Path.Join(_sourceClaude, "CLAUDE.md"));
        CreateFile(Path.Join(_sourceClaude, "skills"));
        CreateFile(Path.Join(_sourceClaude, "commands"));

        ConfigCopyPlanner.Available(_source).ShouldBeEmpty();
    }

    [Fact]
    public void Desktop_config_is_looked_up_in_the_first_existing_candidate_only()
    {
        Directory.CreateDirectory(_msixDesktop);
        CreateFile(Path.Join(_roamingDesktop, "claude_desktop_config.json"));

        ConfigCopyPlanner.Available(_source).ShouldNotContain(CopyItem.DesktopMcpConfig);
    }

    [Fact]
    public void Desktop_config_falls_back_to_a_later_candidate_when_earlier_ones_are_missing()
    {
        CreateFile(Path.Join(_roamingDesktop, "claude_desktop_config.json"));

        var plan = ConfigCopyPlanner.Plan(_source, _target, Request(false, CopyItem.DesktopMcpConfig));

        plan.Operations.ShouldBe([
            new CopyOperation(
                CopyItem.DesktopMcpConfig,
                Path.Join(_roamingDesktop, "claude_desktop_config.json"),
                Path.Join(_target.DesktopUserDataDir!, "claude_desktop_config.json"),
                IsDirectory: false,
                RewritePaths: false),
        ]);
    }

    [Fact]
    public void Desktop_config_prefers_the_first_candidate()
    {
        CreateFile(Path.Join(_msixDesktop, "claude_desktop_config.json"));
        CreateFile(Path.Join(_roamingDesktop, "claude_desktop_config.json"));

        var plan = ConfigCopyPlanner.Plan(_source, _target, Request(false, CopyItem.DesktopMcpConfig));

        plan.Operations.Single().SourcePath.ShouldBe(Path.Join(_msixDesktop, "claude_desktop_config.json"));
    }

    [Fact]
    public void Plan_contains_requested_available_items_in_item_order()
    {
        CreateAllClaudeItems();
        var targetClaude = _target.ConfigDirOverride!;

        var plan = ConfigCopyPlanner.Plan(
            _source, _target, Request(false, CopyItem.Skills, CopyItem.ClaudeMd, CopyItem.DesktopMcpConfig));

        plan.Operations.ShouldBe([
            new CopyOperation(
                CopyItem.ClaudeMd,
                Path.Join(_sourceClaude, "CLAUDE.md"),
                Path.Join(targetClaude, "CLAUDE.md"),
                IsDirectory: false,
                RewritePaths: false),
            new CopyOperation(
                CopyItem.Skills,
                Path.Join(_sourceClaude, "skills"),
                Path.Join(targetClaude, "skills"),
                IsDirectory: true,
                RewritePaths: false),
        ]);
    }

    [Fact]
    public void Only_settings_json_is_marked_for_rewriting_when_requested()
    {
        CreateAllClaudeItems();

        var plan = ConfigCopyPlanner.Plan(
            _source, _target, Request(true, CopyItem.ClaudeMd, CopyItem.SettingsJson, CopyItem.Agents));

        plan.Operations.Select(op => (op.Item, op.RewritePaths)).ShouldBe([
            (CopyItem.ClaudeMd, false),
            (CopyItem.SettingsJson, true),
            (CopyItem.Agents, false),
        ]);
    }

    [Fact]
    public void Settings_json_is_not_rewritten_unless_requested()
    {
        CreateAllClaudeItems();

        var plan = ConfigCopyPlanner.Plan(_source, _target, Request(false, CopyItem.SettingsJson));

        plan.Operations.Single().RewritePaths.ShouldBeFalse();
    }

    [Fact]
    public void Profile_source_is_planned_from_its_own_directories()
    {
        var source = ProfilePaths("firma");
        CreateFile(Path.Join(source.ConfigDirOverride!, "CLAUDE.md"));
        CreateFile(Path.Join(source.DesktopUserDataDir!, "claude_desktop_config.json"));

        var plan = ConfigCopyPlanner.Plan(
            source, _target, Request(false, CopyItem.ClaudeMd, CopyItem.DesktopMcpConfig));

        plan.Operations.Select(op => op.SourcePath).ShouldBe([
            Path.Join(source.ConfigDirOverride!, "CLAUDE.md"),
            Path.Join(source.DesktopUserDataDir!, "claude_desktop_config.json"),
        ]);
    }

    [Fact]
    public void Default_account_is_refused_as_target()
    {
        CreateAllClaudeItems();

        Should.Throw<ArgumentException>(() =>
            ConfigCopyPlanner.Plan(ProfilePaths("firma"), _source, Request(false, CopyItem.ClaudeMd)));
    }
}
