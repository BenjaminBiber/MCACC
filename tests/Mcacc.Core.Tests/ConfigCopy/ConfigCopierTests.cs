using Mcacc.Core.ConfigCopy;
using Mcacc.Core.Paths;
using Mcacc.Core.Tests.TestSupport;
using Shouldly;
using Xunit;

namespace Mcacc.Core.Tests.ConfigCopy;

public sealed class ConfigCopierTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly string _sourceClaude;
    private readonly string _sourceDesktop;
    private readonly string _outside;
    private readonly AccountPaths _source;
    private readonly AccountPaths _target;
    private readonly ConfigCopier _copier = new();

    public ConfigCopierTests()
    {
        _sourceClaude = Path.Join(_temp.Path, "home", ".claude");
        _sourceDesktop = Path.Join(_temp.Path, "roaming", "Claude");
        _outside = Path.Join(_temp.Path, "outside");
        Directory.CreateDirectory(_outside);
        _source = new AccountPaths(
            DesktopUserDataDir: null,
            ClaudeConfigDir: null,
            EffectiveClaudeDir: _sourceClaude,
            ClaudeJsonFile: Path.Join(_temp.Path, "home", ".claude.json"),
            CredentialsFile: Path.Join(_sourceClaude, ".credentials.json"),
            DesktopDataDirs: [_sourceDesktop]);
        _target = ProfilePaths("privat");
    }

    public void Dispose() => _temp.Dispose();

    private string TargetClaude => _target.ClaudeConfigDir!;

    private string TargetDesktop => _target.DesktopUserDataDir!;

    private AccountPaths ProfilePaths(string id)
    {
        var profile = Path.Join(_temp.Path, "mcacc", "profiles", id);
        var claude = Path.Join(profile, "claude");
        var desktop = Path.Join(profile, "desktop");
        return new AccountPaths(
            desktop, claude, claude, Path.Join(claude, ".claude.json"), Path.Join(claude, ".credentials.json"), [desktop]);
    }

    private static void WriteFile(string path, string content = "content")
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private CopyReport Copy(AccountPaths source, bool rewrite, params CopyItem[] items)
    {
        var request = new CopyRequest("standard", items.ToHashSet(), rewrite);
        return _copier.Execute(ConfigCopyPlanner.Plan(source, _target, request), source, _target);
    }

    private CopyReport Copy(params CopyItem[] items) => Copy(_source, rewrite: false, items);

    private static string[] Entries(string dir) =>
        Directory.EnumerateFileSystemEntries(dir, "*", SearchOption.AllDirectories)
            .Select(entry => Path.GetRelativePath(dir, entry))
            .Order(StringComparer.Ordinal)
            .ToArray();

    [Fact]
    public void Files_and_directories_are_copied_recursively()
    {
        WriteFile(Path.Join(_sourceClaude, "CLAUDE.md"), "# memory");
        WriteFile(Path.Join(_sourceClaude, "settings.json"), """{ "model": "opus" }""");
        WriteFile(Path.Join(_sourceClaude, "agents", "reviewer.md"), "agent");
        WriteFile(Path.Join(_sourceClaude, "skills", "kanban", "SKILL.md"), "skill");
        WriteFile(Path.Join(_sourceClaude, "skills", "kanban", "scripts", "board.ps1"), "script");
        Directory.CreateDirectory(Path.Join(_sourceClaude, "skills", "empty"));
        WriteFile(Path.Join(_sourceClaude, "commands", "ship.md"), "command");
        WriteFile(Path.Join(_sourceClaude, "scripts", "statusline.sh"), "status");
        WriteFile(Path.Join(_sourceDesktop, "claude_desktop_config.json"), """{ "mcpServers": {} }""");

        var report = Copy(Enum.GetValues<CopyItem>());

        report.Copied.ShouldBe(Enum.GetValues<CopyItem>());
        report.Skipped.ShouldBeEmpty();
        File.ReadAllText(Path.Join(TargetClaude, "CLAUDE.md")).ShouldBe("# memory");
        File.ReadAllText(Path.Join(TargetClaude, "settings.json")).ShouldBe("""{ "model": "opus" }""");
        File.ReadAllText(Path.Join(TargetClaude, "skills", "kanban", "scripts", "board.ps1")).ShouldBe("script");
        Directory.Exists(Path.Join(TargetClaude, "skills", "empty")).ShouldBeTrue();
        File.ReadAllText(Path.Join(TargetDesktop, "claude_desktop_config.json")).ShouldBe("""{ "mcpServers": {} }""");
        Entries(Path.Join(TargetClaude, "agents")).ShouldBe(["reviewer.md"]);
        Entries(Path.Join(TargetClaude, "commands")).ShouldBe(["ship.md"]);
        Entries(Path.Join(TargetClaude, "scripts")).ShouldBe(["statusline.sh"]);
    }

    [Fact]
    public void Only_the_whitelist_reaches_the_target()
    {
        WriteFile(Path.Join(_sourceClaude, "CLAUDE.md"));
        WriteFile(Path.Join(_sourceClaude, "settings.json"));
        WriteFile(Path.Join(_sourceClaude, "skills", "a", "SKILL.md"));
        WriteFile(Path.Join(_sourceClaude, ".credentials.json"), "secret");
        WriteFile(Path.Join(_sourceClaude, ".claude.json"), "{}");
        WriteFile(Path.Join(_sourceClaude, "settings.local.json"));
        WriteFile(Path.Join(_sourceClaude, "history.jsonl"));
        WriteFile(Path.Join(_sourceClaude, "projects", "p", "session.jsonl"));
        WriteFile(Path.Join(_sourceClaude, "sessions", "s.json"));
        WriteFile(Path.Join(_sourceClaude, "plugins", "installed_plugins.json"));
        WriteFile(Path.Join(_sourceClaude, "todos", "t.json"));
        WriteFile(Path.Join(_sourceDesktop, "claude_desktop_config.json"));
        WriteFile(Path.Join(_sourceDesktop, "config.json"));
        WriteFile(Path.Join(_sourceDesktop, "Local Storage", "leveldb", "000003.log"));

        Copy(Enum.GetValues<CopyItem>());

        Entries(TargetClaude).ShouldBe([
            "CLAUDE.md",
            "settings.json",
            "skills",
            Path.Join("skills", "a"),
            Path.Join("skills", "a", "SKILL.md"),
        ]);
        Entries(TargetDesktop).ShouldBe(["claude_desktop_config.json"]);
    }

    [Fact]
    public void Existing_target_file_is_never_overwritten()
    {
        WriteFile(Path.Join(_sourceClaude, "CLAUDE.md"), "theirs");
        WriteFile(Path.Join(_sourceClaude, "settings.json"), "theirs");
        WriteFile(Path.Join(TargetClaude, "CLAUDE.md"), "mine");
        WriteFile(Path.Join(TargetClaude, "settings.json"), "mine");

        var report = Copy(_source, rewrite: true, CopyItem.ClaudeMd, CopyItem.SettingsJson);

        report.Copied.ShouldBeEmpty();
        report.Skipped.ShouldBe([
            new SkippedEntry("CLAUDE.md", CopySkipReason.TargetExists),
            new SkippedEntry("settings.json", CopySkipReason.TargetExists),
        ]);
        File.ReadAllText(Path.Join(TargetClaude, "CLAUDE.md")).ShouldBe("mine");
        File.ReadAllText(Path.Join(TargetClaude, "settings.json")).ShouldBe("mine");
    }

    [Fact]
    public void Existing_target_directory_is_merged_without_overwriting()
    {
        WriteFile(Path.Join(_sourceClaude, "skills", "a", "SKILL.md"), "theirs");
        WriteFile(Path.Join(_sourceClaude, "skills", "b", "SKILL.md"), "new");
        WriteFile(Path.Join(TargetClaude, "skills", "a", "SKILL.md"), "mine");

        var report = Copy(CopyItem.Skills);

        report.Copied.ShouldBe([CopyItem.Skills]);
        report.Skipped.ShouldBe([new SkippedEntry(Path.Join("skills", "a", "SKILL.md"), CopySkipReason.TargetExists)]);
        File.ReadAllText(Path.Join(TargetClaude, "skills", "a", "SKILL.md")).ShouldBe("mine");
        File.ReadAllText(Path.Join(TargetClaude, "skills", "b", "SKILL.md")).ShouldBe("new");
    }

    [Fact]
    public void Target_of_the_other_kind_counts_as_existing()
    {
        WriteFile(Path.Join(_sourceClaude, "agents", "a.md"));
        WriteFile(Path.Join(TargetClaude, "agents"), "a file");

        var report = Copy(CopyItem.Agents);

        report.Skipped.ShouldBe([new SkippedEntry("agents", CopySkipReason.TargetExists)]);
        File.ReadAllText(Path.Join(TargetClaude, "agents")).ShouldBe("a file");
    }

    [Fact]
    public void Source_that_vanished_after_planning_is_reported_missing()
    {
        WriteFile(Path.Join(_sourceClaude, "CLAUDE.md"));
        Directory.CreateDirectory(Path.Join(_sourceClaude, "skills"));
        var plan = ConfigCopyPlanner.Plan(
            _source, _target, new CopyRequest("standard", new HashSet<CopyItem> { CopyItem.ClaudeMd, CopyItem.Skills }, false));
        File.Delete(Path.Join(_sourceClaude, "CLAUDE.md"));
        Directory.Delete(Path.Join(_sourceClaude, "skills"));

        var report = _copier.Execute(plan, _source, _target);

        report.Copied.ShouldBeEmpty();
        report.Skipped.ShouldBe([
            new SkippedEntry("CLAUDE.md", CopySkipReason.SourceMissing),
            new SkippedEntry("skills", CopySkipReason.SourceMissing),
        ]);
        Directory.Exists(Path.Join(TargetClaude, "skills")).ShouldBeFalse();
    }

    [Fact]
    public void Settings_of_the_default_account_are_rewritten_when_requested()
    {
        var sourceSlashed = _sourceClaude.Replace('\\', '/');
        var targetSlashed = TargetClaude.Replace('\\', '/');
        WriteFile(
            Path.Join(_sourceClaude, "settings.json"),
            $$"""{ "a": "bash ~/.claude/x.sh", "b": "{{sourceSlashed}}/y.sh" }""");

        var report = Copy(_source, rewrite: true, CopyItem.SettingsJson);

        report.Copied.ShouldBe([CopyItem.SettingsJson]);
        File.ReadAllText(Path.Join(TargetClaude, "settings.json"))
            .ShouldBe($$"""{ "a": "bash {{targetSlashed}}/x.sh", "b": "{{targetSlashed}}/y.sh" }""");
    }

    [Fact]
    public void Settings_of_a_profile_keep_home_relative_references()
    {
        var source = ProfilePaths("firma");
        var sourceSlashed = source.ClaudeConfigDir!.Replace('\\', '/');
        var targetSlashed = TargetClaude.Replace('\\', '/');
        WriteFile(
            Path.Join(source.ClaudeConfigDir!, "settings.json"),
            $$"""{ "a": "bash ~/.claude/x.sh", "b": "{{sourceSlashed}}/y.sh" }""");

        Copy(source, rewrite: true, CopyItem.SettingsJson);

        File.ReadAllText(Path.Join(TargetClaude, "settings.json"))
            .ShouldBe($$"""{ "a": "bash ~/.claude/x.sh", "b": "{{targetSlashed}}/y.sh" }""");
    }

    [Fact]
    public void Settings_are_copied_verbatim_without_rewrite()
    {
        var content = $$"""{ "a": "bash ~/.claude/x.sh", "b": "{{_sourceClaude.Replace('\\', '/')}}/y.sh" }""";
        WriteFile(Path.Join(_sourceClaude, "settings.json"), content);

        Copy(_source, rewrite: false, CopyItem.SettingsJson);

        File.ReadAllText(Path.Join(TargetClaude, "settings.json")).ShouldBe(content);
    }

    [Fact]
    public void Hidden_entries_inside_directories_are_copied()
    {
        var hidden = Path.Join(_sourceClaude, "skills", ".hidden");
        WriteFile(hidden);
        File.SetAttributes(hidden, File.GetAttributes(hidden) | FileAttributes.Hidden);

        Copy(CopyItem.Skills);

        File.Exists(Path.Join(TargetClaude, "skills", ".hidden")).ShouldBeTrue();
    }

    [Fact]
    public void Linked_item_directory_is_skipped()
    {
        WriteFile(Path.Join(_outside, "SKILL.md"));
        Directory.CreateDirectory(_sourceClaude);
        FileSystemLinks.CreateDirectoryLink(Path.Join(_sourceClaude, "skills"), _outside);

        var report = Copy(CopyItem.Skills);

        report.Copied.ShouldBeEmpty();
        report.Skipped.ShouldBe([new SkippedEntry("skills", CopySkipReason.ReparsePoint)]);
        Directory.Exists(Path.Join(TargetClaude, "skills")).ShouldBeFalse();
    }

    [Fact]
    public void Linked_directory_inside_an_item_is_skipped()
    {
        WriteFile(Path.Join(_outside, "SKILL.md"));
        WriteFile(Path.Join(_sourceClaude, "skills", "own", "SKILL.md"));
        FileSystemLinks.CreateDirectoryLink(Path.Join(_sourceClaude, "skills", "linked"), _outside);

        var report = Copy(CopyItem.Skills);

        report.Copied.ShouldBe([CopyItem.Skills]);
        report.Skipped.ShouldBe([new SkippedEntry(Path.Join("skills", "linked"), CopySkipReason.ReparsePoint)]);
        Entries(Path.Join(TargetClaude, "skills")).ShouldBe(["own", Path.Join("own", "SKILL.md")]);
    }

    [Fact]
    public void Linked_file_is_skipped()
    {
        WriteFile(Path.Join(_outside, "settings.json"));
        Directory.CreateDirectory(_sourceClaude);
        FileSystemLinks.CreateFileLink(Path.Join(_sourceClaude, "settings.json"), Path.Join(_outside, "settings.json"));

        var report = Copy(CopyItem.SettingsJson);

        report.Skipped.ShouldBe([new SkippedEntry("settings.json", CopySkipReason.ReparsePoint)]);
        File.Exists(Path.Join(TargetClaude, "settings.json")).ShouldBeFalse();
    }

    [Fact]
    public void Linked_target_directory_is_not_written_through()
    {
        WriteFile(Path.Join(_sourceClaude, "skills", "a", "SKILL.md"));
        Directory.CreateDirectory(TargetClaude);
        FileSystemLinks.CreateDirectoryLink(Path.Join(TargetClaude, "skills"), _outside);

        var report = Copy(CopyItem.Skills);

        report.Skipped.ShouldBe([new SkippedEntry("skills", CopySkipReason.ReparsePoint)]);
        Directory.EnumerateFileSystemEntries(_outside).ShouldBeEmpty();
    }

    [Fact]
    public void Default_account_is_refused_as_target()
    {
        var plan = new CopyPlan([
            new CopyOperation(
                CopyItem.ClaudeMd,
                Path.Join(TargetClaude, "CLAUDE.md"),
                Path.Join(_sourceClaude, "CLAUDE.md"),
                IsDirectory: false,
                RewritePaths: false),
        ]);
        WriteFile(Path.Join(TargetClaude, "CLAUDE.md"));

        Should.Throw<ArgumentException>(() => _copier.Execute(plan, _target, _source));
        File.Exists(Path.Join(_sourceClaude, "CLAUDE.md")).ShouldBeFalse();
    }
}
