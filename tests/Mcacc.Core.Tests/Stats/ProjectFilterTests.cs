using Mcacc.Core.Stats;
using Shouldly;
using Xunit;

namespace Mcacc.Core.Tests.Stats;

public class ProjectFilterTests
{
    private static readonly DateTimeOffset Hour = new(2026, 9, 25, 8, 0, 0, TimeSpan.Zero);

    private static TokenBucket Bucket(string project) =>
        new(Hour, "claude-opus-4-5", project, "s1", new TokenCounts(1, 1, 0, 0, 0), 1);

    [Fact]
    public void An_excluded_project_is_left_out_whatever_its_case_and_trailing_separator()
    {
        var buckets = new[] { Bucket(@"C:\Repos\Spielwiese"), Bucket(@"C:\Repos\MCACC") };

        var left = ProjectFilter.Without(buckets, cwd => cwd, [@"c:\repos\spielwiese\"]);

        left.ShouldHaveSingleItem().Project.ShouldBe(@"C:\Repos\MCACC");
    }

    [Fact]
    public void A_worktree_goes_with_its_project()
    {
        var buckets = new[] { Bucket("/Users/ben/repos/app/.claude/worktrees/fix-1"), Bucket("/Users/ben/repos/other") };

        var left = ProjectFilter.Without(buckets, ProjectRoot.Of, ["/Users/ben/repos/app"]);

        left.ShouldHaveSingleItem().Project.ShouldBe("/Users/ben/repos/other");
    }

    [Fact]
    public void Without_an_exclusion_the_list_stays_the_same()
    {
        var buckets = new[] { Bucket("/a") };

        ProjectFilter.Without(buckets, cwd => cwd, null).ShouldBeSameAs(buckets);
        ProjectFilter.Without(buckets, cwd => cwd, []).ShouldBeSameAs(buckets);
    }

    [Fact]
    public void Contains_compares_like_the_filter()
    {
        ProjectFilter.Contains([@"C:\Repos\A"], @"c:\repos\a\").ShouldBeTrue();
        ProjectFilter.Contains([@"C:\Repos\A"], @"C:\Repos\AB").ShouldBeFalse();
        ProjectFilter.Contains(null, @"C:\Repos\A").ShouldBeFalse();
    }

    [Fact]
    public void A_root_keeps_its_separator()
    {
        ProjectFilter.Normalize("/").ShouldBe("/");
        ProjectFilter.Normalize(@"C:\Repos\A\").ShouldBe(@"C:\Repos\A");
    }
}
