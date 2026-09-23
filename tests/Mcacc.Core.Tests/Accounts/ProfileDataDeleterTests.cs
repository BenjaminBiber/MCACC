using Mcacc.Core.Accounts;
using Mcacc.Core.Model;
using Mcacc.Core.Paths;
using Mcacc.Core.Tests.TestSupport;
using Shouldly;
using Xunit;

namespace Mcacc.Core.Tests.Accounts;

public sealed class ProfileDataDeleterTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly McaccPaths _paths;
    private readonly ProfileDataDeleter _deleter = new();

    public ProfileDataDeleterTests()
    {
        _paths = new McaccPaths(Path.Join(_temp.Path, "mcacc"), PlatformInfo.Current().PathStyle);
        WriteFile(_paths.ConfigFile);
    }

    public void Dispose() => _temp.Dispose();

    private static Account Profile(string id) => new(id, id, "#6A9BCC", IsDefault: false);

    private static void WriteFile(string path, FileAttributes extra = 0)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "data");
        File.SetAttributes(path, File.GetAttributes(path) | extra);
    }

    [Fact]
    public void Default_account_is_refused()
    {
        Should.Throw<InvalidOperationException>(() => _deleter.Delete(Account.CreateDefault(), _paths));
    }

    [Fact]
    public void Default_account_is_refused_even_with_another_id()
    {
        var renamedDefault = new Account("work", "Firma", "#D97757", IsDefault: true);
        WriteFile(Path.Join(_paths.ProfileDir("work"), "claude", "CLAUDE.md"));

        Should.Throw<InvalidOperationException>(() => _deleter.Delete(renamedDefault, _paths));
        Directory.Exists(_paths.ProfileDir("work")).ShouldBeTrue();
    }

    [Theory]
    [InlineData("..")]
    [InlineData("../..")]
    [InlineData("privat/../..")]
    [InlineData("privat/..")]
    [InlineData(".")]
    [InlineData("")]
    [InlineData("../profiles-evil")]
    [InlineData("...")]
    [InlineData(" ")]
    [InlineData(". .")]
    [InlineData("privat.")]
    [InlineData("privat ")]
    public void Unsafe_id_is_refused_and_nothing_is_deleted(string id)
    {
        WriteFile(Path.Join(_paths.ProfileDir("privat"), "claude", "CLAUDE.md"));
        WriteFile(Path.Join(_paths.ProfileDir("firma"), "claude", "CLAUDE.md"));
        WriteFile(Path.Join(_paths.Root, "profiles-evil", "keep.txt"));

        Should.Throw<InvalidOperationException>(() => _deleter.Delete(Profile(id), _paths));

        File.Exists(_paths.ConfigFile).ShouldBeTrue();
        File.Exists(Path.Join(_paths.ProfileDir("privat"), "claude", "CLAUDE.md")).ShouldBeTrue();
        File.Exists(Path.Join(_paths.ProfileDir("firma"), "claude", "CLAUDE.md")).ShouldBeTrue();
        File.Exists(Path.Join(_paths.Root, "profiles-evil", "keep.txt")).ShouldBeTrue();
    }

    [Fact]
    public void Profile_directory_is_deleted_and_nothing_else()
    {
        WriteFile(Path.Join(_paths.DesktopDataDir("privat"), "Local Storage", "leveldb", "000003.log"));
        WriteFile(Path.Join(_paths.ClaudeConfigDir("privat"), ".credentials.json"));
        WriteFile(Path.Join(_paths.ClaudeConfigDir("privat-2"), "CLAUDE.md"));

        _deleter.Delete(Profile("privat"), _paths);

        Directory.Exists(_paths.ProfileDir("privat")).ShouldBeFalse();
        File.Exists(Path.Join(_paths.ClaudeConfigDir("privat-2"), "CLAUDE.md")).ShouldBeTrue();
        File.Exists(_paths.ConfigFile).ShouldBeTrue();
    }

    [Fact]
    public void Read_only_and_hidden_entries_are_deleted()
    {
        var claude = _paths.ClaudeConfigDir("privat");
        WriteFile(Path.Join(claude, "settings.json"), FileAttributes.ReadOnly);
        WriteFile(Path.Join(claude, ".hidden"), FileAttributes.ReadOnly | FileAttributes.Hidden);
        var lockedDir = Path.Join(claude, "plugins");
        WriteFile(Path.Join(lockedDir, "plugin.json"), FileAttributes.ReadOnly);
        File.SetAttributes(lockedDir, File.GetAttributes(lockedDir) | FileAttributes.ReadOnly);

        _deleter.Delete(Profile("privat"), _paths);

        Directory.Exists(_paths.ProfileDir("privat")).ShouldBeFalse();
    }

    [Fact]
    public void Missing_profile_directory_is_not_an_error()
    {
        Should.NotThrow(() => _deleter.Delete(Profile("privat"), _paths));
    }

    [Fact]
    public void Linked_directory_inside_the_profile_is_removed_without_touching_its_target()
    {
        var outside = Path.Join(_temp.Path, "home", ".claude", "skills");
        WriteFile(Path.Join(outside, "kanban", "SKILL.md"), FileAttributes.ReadOnly);
        WriteFile(Path.Join(_paths.ClaudeConfigDir("privat"), "CLAUDE.md"));
        FileSystemLinks.CreateDirectoryLink(Path.Join(_paths.ClaudeConfigDir("privat"), "skills"), outside);

        _deleter.Delete(Profile("privat"), _paths);

        Directory.Exists(_paths.ProfileDir("privat")).ShouldBeFalse();
        var kept = Path.Join(outside, "kanban", "SKILL.md");
        File.Exists(kept).ShouldBeTrue();
        File.GetAttributes(kept).HasFlag(FileAttributes.ReadOnly).ShouldBeTrue();
    }

    [Fact]
    public void Linked_profile_directory_is_removed_without_touching_its_target()
    {
        var outside = Path.Join(_temp.Path, "elsewhere");
        WriteFile(Path.Join(outside, "claude", "CLAUDE.md"), FileAttributes.ReadOnly);
        Directory.CreateDirectory(_paths.ProfilesDir);
        FileSystemLinks.CreateDirectoryLink(_paths.ProfileDir("privat"), outside);

        _deleter.Delete(Profile("privat"), _paths);

        new DirectoryInfo(_paths.ProfileDir("privat")).LinkTarget.ShouldBeNull();
        Directory.Exists(_paths.ProfileDir("privat")).ShouldBeFalse();
        var kept = Path.Join(outside, "claude", "CLAUDE.md");
        File.Exists(kept).ShouldBeTrue();
        File.GetAttributes(kept).HasFlag(FileAttributes.ReadOnly).ShouldBeTrue();
    }
}
