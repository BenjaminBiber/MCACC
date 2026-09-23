using Mcacc.Core.Paths;
using Shouldly;
using Xunit;

namespace Mcacc.Core.Tests.Paths;

public class PathComparerTests
{
    [Theory]
    [InlineData(@"C:\Users\ben\", @"C:\Users\ben")]
    [InlineData(@"C:\Users\ben\\\", @"C:\Users\ben")]
    [InlineData("C:/Users//ben/", @"C:\Users\ben")]
    [InlineData(@"C:\Users/ben\\.mcacc", @"C:\Users\ben\.mcacc")]
    [InlineData(@"C:\", @"C:\")]
    [InlineData(@"C:\\", @"C:\")]
    [InlineData("C:/", @"C:\")]
    [InlineData(@"\", @"\")]
    [InlineData(@"\\server\share\dir\", @"\\server\share\dir")]
    [InlineData("//server//share/", @"\\server\share")]
    public void Normalize_windows(string path, string expected)
    {
        PathComparer.Normalize(path, PathStyle.Windows).ShouldBe(expected);
    }

    [Theory]
    [InlineData("/Users/ben/", "/Users/ben")]
    [InlineData("/Users//ben///", "/Users/ben")]
    [InlineData("//Users/ben", "/Users/ben")]
    [InlineData("/", "/")]
    [InlineData("///", "/")]
    [InlineData(@"/Users/ben\x", @"/Users/ben\x")]
    public void Normalize_posix(string path, string expected)
    {
        PathComparer.Normalize(path, PathStyle.Posix).ShouldBe(expected);
    }

    [Theory]
    [InlineData(@"C:\Users\Ben\AppData\Roaming\Claude", @"c:\users\ben\appdata\roaming\claude\")]
    [InlineData(@"C:\Users\ben\.mcacc\profiles\privat\desktop", "C:/Users/ben/.mcacc/profiles/privat/desktop")]
    [InlineData(@"C:\Users\ben", @"C:\\Users\\ben\\")]
    public void Same_windows_ignores_case_separators_and_trailing_separators(string a, string b)
    {
        PathComparer.Same(a, b, PathStyle.Windows).ShouldBeTrue();
    }

    [Theory]
    [InlineData("/Users/Ben/Library/Application Support/Claude", "/users/ben/library/application support/claude/")]
    [InlineData("/Users/ben//.mcacc", "/Users/ben/.mcacc")]
    public void Same_posix_ignores_case_and_duplicate_or_trailing_separators(string a, string b)
    {
        PathComparer.Same(a, b, PathStyle.Posix).ShouldBeTrue();
    }

    [Theory]
    [InlineData(@"C:\Users\ben\a", @"C:\Users\ben\b", PathStyle.Windows)]
    [InlineData(@"C:\Users\ben\a", @"C:\Users\ben\ab", PathStyle.Windows)]
    [InlineData("/Users/ben/a", "/Users/ben/b", PathStyle.Posix)]
    [InlineData(@"/Users/ben\a", "/Users/ben/a", PathStyle.Posix)]
    public void Same_is_false_for_different_paths(string a, string b, PathStyle style)
    {
        PathComparer.Same(a, b, style).ShouldBeFalse();
    }

    [Theory]
    [InlineData(@"C:\m\profiles\privat", @"C:\m\profiles")]
    [InlineData(@"C:\m\profiles\privat\desktop", @"C:\m\profiles\")]
    [InlineData(@"c:/M/Profiles/privat", @"C:\m\profiles")]
    [InlineData(@"C:\m\profiles\a\..\privat", @"C:\m\profiles")]
    [InlineData(@"C:\m\profiles\.\privat", @"C:\m\profiles")]
    [InlineData(@"C:\m\x\..\profiles\privat", @"C:\m\profiles")]
    [InlineData(@"C:\m\profiles\privat", @"C:\m\x\..\profiles")]
    [InlineData(@"C:\m", @"C:\")]
    public void IsStrictlyUnder_windows_accepts_descendants(string child, string parent)
    {
        PathComparer.IsStrictlyUnder(child, parent, PathStyle.Windows).ShouldBeTrue();
    }

    [Theory]
    [InlineData(@"C:\m\profiles", @"C:\m\profiles")]
    [InlineData(@"C:\m\profiles\", @"C:\m\profiles")]
    [InlineData(@"C:\M\PROFILES", @"C:\m\profiles\")]
    [InlineData(@"C:\m\profiles\privat\..", @"C:\m\profiles")]
    [InlineData(@"C:\m\profiles\..", @"C:\m\profiles")]
    [InlineData(@"C:\m\profiles\..\config.json", @"C:\m\profiles")]
    [InlineData(@"C:\m\profiles\privat\..\..\..\Users", @"C:\m\profiles")]
    [InlineData(@"C:\m\profiles-evil\privat", @"C:\m\profiles")]
    [InlineData(@"C:\m\profilesx", @"C:\m\profiles")]
    [InlineData(@"C:\m", @"C:\m\profiles")]
    [InlineData(@"D:\m\profiles\privat", @"C:\m\profiles")]
    [InlineData(@"C:\..\..\m\profiles", @"C:\m\profiles")]
    [InlineData(@"profiles\privat", @"C:\m\profiles")]
    [InlineData("privat", "")]
    public void IsStrictlyUnder_windows_rejects_equal_escaping_and_sibling_paths(string child, string parent)
    {
        PathComparer.IsStrictlyUnder(child, parent, PathStyle.Windows).ShouldBeFalse();
    }

    [Theory]
    [InlineData("/Users/ben/.mcacc/profiles/privat", "/Users/ben/.mcacc/profiles", true)]
    [InlineData("/Users/ben/.mcacc/profiles/a/../privat", "/Users/ben/.mcacc/profiles/", true)]
    [InlineData("/Users/Ben/.MCACC/profiles/privat", "/users/ben/.mcacc/profiles", true)]
    [InlineData("/Users", "/", true)]
    [InlineData("/Users/ben/.mcacc/profiles", "/Users/ben/.mcacc/profiles/", false)]
    [InlineData("/Users/ben/.mcacc/profiles/..", "/Users/ben/.mcacc/profiles", false)]
    [InlineData("/Users/ben/.mcacc/profiles/../config.json", "/Users/ben/.mcacc/profiles", false)]
    [InlineData("/Users/ben/.mcacc/profiles-evil", "/Users/ben/.mcacc/profiles", false)]
    [InlineData(@"/Users/ben/.mcacc/profiles\privat", "/Users/ben/.mcacc/profiles", false)]
    [InlineData("/", "/", false)]
    public void IsStrictlyUnder_posix(string child, string parent, bool expected)
    {
        PathComparer.IsStrictlyUnder(child, parent, PathStyle.Posix).ShouldBe(expected);
    }
}
