using Mcacc.Core.ConfigCopy;
using Mcacc.Core.Model;
using Mcacc.Core.Paths;
using Mcacc.Core.Tests.TestSupport;
using Shouldly;
using Xunit;

namespace Mcacc.Core.Tests.ConfigCopy;

/// <summary>
/// Copying a Codex account against the real shape of <c>config.toml</c>. The fixture is the file off this machine
/// with the user name replaced, so the cases are the ones that actually occur.
/// </summary>
public sealed class CodexConfigCopyTests : IDisposable
{
    private const string SourceHome = @"C:\Users\x\.codex";

    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    private static AccountPaths Paths(string dir, bool isDefault, ProviderKind provider = ProviderKind.Codex) => new(
        ProviderDescriptors.For(provider),
        DesktopUserDataDir: Path.Join(dir, "..", "codex-desktop"),
        ConfigDirOverride: isDefault ? null : dir,
        EffectiveConfigDir: dir,
        IdentityFile: Path.Join(dir, "auth.json"),
        CredentialsFile: Path.Join(dir, "auth.json"),
        DesktopDataDirs: []);

    private string TargetHome => Path.Join(_temp.Path, "profiles", "codex-privat", "codex");

    private string Rewritten()
    {
        var toml = FixtureFiles.ReadAllText("codex-config.toml");
        return ProfilePathRewriter.Rewrite(
            toml,
            Paths(SourceHome, isDefault: true),
            Paths(TargetHome, isDefault: false));
    }

    /// <summary>
    /// The whole reason the rewrite is mandatory: the file sets CODEX_HOME itself. Left alone, a brand-new profile
    /// would hand the old home to every MCP server it starts.
    /// </summary>
    [Fact]
    public void No_path_in_the_real_config_still_points_at_the_source_home()
    {
        var rewritten = Rewritten();

        rewritten.ShouldNotContain(SourceHome, Case.Insensitive);
        rewritten.ShouldNotContain(SourceHome.Replace('\\', '/'), Case.Insensitive);
        rewritten.ShouldContain($"CODEX_HOME = '{TargetHome}'");
    }

    [Fact]
    public void The_long_path_marketplace_source_keeps_its_prefix()
    {
        Rewritten().ShouldContain(@"source = '\\?\" + TargetHome + @"\.tmp\bundled-marketplaces\openai-bundled'");
    }

    [Fact]
    public void A_forward_slashed_path_inside_a_json_string_is_rewritten_too()
    {
        Rewritten().ShouldContain(TargetHome.Replace('\\', '/') + "/plugins/cache/");
    }

    /// <summary>
    /// A real config.toml mixes both string kinds: single-quoted literals with plain backslashes and double-quoted
    /// basic strings with doubled ones. A match has to come back in the escaping of the string it sat in — doubling
    /// a literal or un-doubling a basic string would break that path either way.
    /// </summary>
    [Fact]
    public void Each_string_kind_keeps_its_own_escaping()
    {
        const string sep = @"\";
        var literal = $"literal = '{SourceHome}{sep}skills'";
        var basic = $"basic = \"{Doubled(SourceHome)}{Doubled(sep)}skills\"";

        var rewritten = ProfilePathRewriter.Rewrite(
            $"{literal}\n{basic}\n",
            Paths(SourceHome, isDefault: false),
            Paths(TargetHome, isDefault: false));

        rewritten.ShouldContain($"literal = '{TargetHome}{sep}skills'");
        rewritten.ShouldContain($"basic = \"{Doubled(TargetHome)}{Doubled(sep)}skills\"");
    }

    private static string Doubled(string path) => path.Replace(@"\", @"\\");

    /// <summary>
    /// The project tables name the user's own folders and carry trust decisions about them; they lie outside the
    /// config home and must come through untouched.
    /// </summary>
    [Fact]
    public void The_project_tables_come_through_untouched()
    {
        var rewritten = Rewritten();

        rewritten.ShouldContain(@"[projects.'c:\users\x']");
        rewritten.ShouldContain(@"[projects.'c:\users\x\riderprojects\agriso']");
    }

    [Fact]
    public void The_config_is_planned_with_a_mandatory_rewrite_whatever_the_user_checked()
    {
        var source = Paths(Path.Join(_temp.Path, "source", "codex"), isDefault: false);
        Directory.CreateDirectory(source.EffectiveConfigDir);
        File.WriteAllText(Path.Join(source.EffectiveConfigDir, "config.toml"), "a = 1\n");
        var request = new CopyRequest("source", new HashSet<CopyItem> { CopyItem.CodexConfigToml }, RewriteSettingsPaths: false);

        var plan = ConfigCopyPlanner.Plan(source, Paths(TargetHome, isDefault: false), request);

        plan.Operations.ShouldHaveSingleItem().RewritePaths.ShouldBeTrue();
    }

    /// <summary>A Claude source must never offer the Codex items, nor the other way round.</summary>
    [Fact]
    public void Each_provider_only_offers_its_own_items()
    {
        var codex = Paths(Path.Join(_temp.Path, "c", "codex"), isDefault: false);
        var claude = Paths(Path.Join(_temp.Path, "a", "claude"), isDefault: false, ProviderKind.Claude);
        Directory.CreateDirectory(codex.EffectiveConfigDir);
        Directory.CreateDirectory(claude.EffectiveConfigDir);

        // The same name under both homes: skills is a Codex item in one and a Claude item in the other.
        Directory.CreateDirectory(Path.Join(codex.EffectiveConfigDir, "skills"));
        Directory.CreateDirectory(Path.Join(claude.EffectiveConfigDir, "skills"));

        ConfigCopyPlanner.Available(codex).ShouldBe([CopyItem.CodexSkills]);
        ConfigCopyPlanner.Available(claude).ShouldBe([CopyItem.Skills]);
    }

    [Fact]
    public void Planning_across_providers_is_refused()
    {
        var codex = Paths(Path.Join(_temp.Path, "c", "codex"), isDefault: false);
        var claude = Paths(TargetHome, isDefault: false, ProviderKind.Claude);
        var request = new CopyRequest("source", new HashSet<CopyItem> { CopyItem.CodexConfigToml }, false);

        Should.Throw<ArgumentException>(() => ConfigCopyPlanner.Plan(codex, claude, request));
    }

    [Fact]
    public void Copying_across_providers_is_refused()
    {
        var codex = Paths(Path.Join(_temp.Path, "c", "codex"), isDefault: false);
        var claude = Paths(TargetHome, isDefault: false, ProviderKind.Claude);

        Should.Throw<ArgumentException>(() => new ConfigCopier().Execute(new CopyPlan([]), codex, claude));
    }

    /// <summary>
    /// Machine-specific paths and hundreds of megabytes of binaries. They exist in every real Codex home, so the
    /// guard is that they are never even offered.
    /// </summary>
    [Theory]
    [InlineData("plugins")]
    [InlineData("packages")]
    [InlineData("vendor_imports")]
    [InlineData("cache")]
    [InlineData("sessions")]
    [InlineData("auth.json")]
    public void What_must_never_be_copied_is_not_offered(string name)
    {
        var source = Paths(Path.Join(_temp.Path, "source", "codex"), isDefault: false);
        Directory.CreateDirectory(source.EffectiveConfigDir);
        var path = Path.Join(source.EffectiveConfigDir, name);
        if (Path.GetExtension(name) is "")
        {
            Directory.CreateDirectory(path);
        }
        else
        {
            File.WriteAllText(path, "{}");
        }

        var available = ConfigCopyPlanner.Available(source);

        available.ShouldAllBe(item => CopyItems.Info(item).RelativePath != name);
    }
}
