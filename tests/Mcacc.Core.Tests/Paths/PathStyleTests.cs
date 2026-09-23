using Mcacc.Core.Paths;
using Shouldly;
using Xunit;

namespace Mcacc.Core.Tests.Paths;

public class PathStyleTests
{
    [Fact]
    public void Separator_is_backslash_for_windows_and_slash_for_posix()
    {
        PathStyle.Windows.Separator().ShouldBe('\\');
        PathStyle.Posix.Separator().ShouldBe('/');
    }

    [Theory]
    [InlineData(new[] { @"C:\Users\ben", ".mcacc" }, @"C:\Users\ben\.mcacc")]
    [InlineData(new[] { @"C:\Users\ben\", @"\.mcacc" }, @"C:\Users\ben\.mcacc")]
    [InlineData(new[] { @"C:\", "Users" }, @"C:\Users")]
    [InlineData(new[] { "C:/Users/ben/", "/.mcacc", "profiles" }, @"C:/Users/ben\.mcacc\profiles")]
    [InlineData(new[] { @"C:\root", "", "config.json" }, @"C:\root\config.json")]
    [InlineData(new[] { @"C:\root\\", @"\\profiles\\" }, @"C:\root\profiles\\")]
    public void Join_windows_uses_backslash_and_trims_duplicate_separators_at_joints(string[] parts, string expected)
    {
        PathStyle.Windows.Join(parts).ShouldBe(expected);
    }

    [Theory]
    [InlineData(new[] { "/Users/ben", ".mcacc" }, "/Users/ben/.mcacc")]
    [InlineData(new[] { "/Users/ben/", "/.mcacc/", "/profiles" }, "/Users/ben/.mcacc/profiles")]
    [InlineData(new[] { "/", "Users" }, "/Users")]
    [InlineData(new[] { "/Users/ben", "Library", "Application Support", "Claude" }, "/Users/ben/Library/Application Support/Claude")]
    [InlineData(new[] { @"/a\", "b" }, @"/a\/b")]
    public void Join_posix_uses_slash_and_keeps_backslashes_as_name_characters(string[] parts, string expected)
    {
        PathStyle.Posix.Join(parts).ShouldBe(expected);
    }

    [Fact]
    public void Join_without_parts_is_empty()
    {
        PathStyle.Posix.Join().ShouldBe("");
    }
}
