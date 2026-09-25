using Mcacc.Core.Stats;
using Shouldly;
using Xunit;

namespace Mcacc.Core.Tests.Stats;

public class ProjectRootTests
{
    [Theory]
    [InlineData(@"C:\Code\Cows\.claude\worktrees\kpis-d4b9ad", @"C:\Code\Cows", @"C:\Code\Cows\.claude\worktrees\kpis-d4b9ad")]
    [InlineData(@"C:\Code\Cows\.claude\worktrees\kpis-d4b9ad\src\app", @"C:\Code\Cows", @"C:\Code\Cows\.claude\worktrees\kpis-d4b9ad")]
    [InlineData(@"C:\Code\Cows\.Claude\Worktrees\kpis", @"C:\Code\Cows", @"C:\Code\Cows\.Claude\Worktrees\kpis")]
    [InlineData("/home/ben/shop/.claude/worktrees/fix-1/", "/home/ben/shop", "/home/ben/shop/.claude/worktrees/fix-1")]
    [InlineData("/home/ben/shop/.worktrees/feature", "/home/ben/shop", "/home/ben/shop/.worktrees/feature")]
    public void A_path_in_a_worktree_belongs_to_its_repository(string cwd, string repository, string worktree)
    {
        ProjectRoot.Of(cwd).ShouldBe(repository);
        ProjectRoot.WorktreeOf(cwd).ShouldBe(worktree);
    }

    [Theory]
    [InlineData(@"C:\Code\Cows")]
    [InlineData(@"C:\Code\Cows\4Cows-FE")]
    [InlineData(@"C:\Code\Cows\.claude\worktrees")]
    [InlineData(@"C:\Code\Cows\.claude")]
    [InlineData(@"C:\Code\worktrees\kpis")]
    [InlineData("/.claude/worktrees/fix-1")]
    [InlineData("")]
    public void Any_other_path_stays_as_it_is(string cwd)
    {
        ProjectRoot.Of(cwd).ShouldBe(cwd);
        ProjectRoot.WorktreeOf(cwd).ShouldBeNull();
    }

    private static readonly HashSet<string> Repositories = [@"C:\Code\Cows", @"C:\Code\Cows\Vendor\Lib", "/home/ben/shop"];

    [Theory]
    [InlineData(@"C:\Code\Cows", @"C:\Code\Cows")]
    [InlineData(@"C:\Code\Cows\4Cows-FE\Components\", @"C:\Code\Cows")]
    [InlineData(@"C:\Code\Cows\.claude\worktrees\kpis\src", @"C:\Code\Cows")]
    [InlineData(@"C:\Code\Cows\Vendor\Lib\src", @"C:\Code\Cows\Vendor\Lib")]
    [InlineData("/home/ben/shop/src/app", "/home/ben/shop")]
    [InlineData(@"C:\Kanban\board", @"C:\Kanban\board")]
    [InlineData(@"C:\Other\.claude\worktrees\fix", @"C:\Other")]
    public void A_session_counts_to_the_nearest_repository_above_it(string cwd, string repository)
    {
        ProjectRoot.RepositoryOf(cwd, Repositories.Contains).ShouldBe(repository);
    }

    [Fact]
    public void Resolve_finds_the_repository_on_disk_even_below_a_deleted_folder()
    {
        var repository = Directory.CreateTempSubdirectory("mcacc-repo-").FullName;
        try
        {
            Directory.CreateDirectory(Path.Join(repository, ".git"));
            var gone = Path.Join(repository, "gone", "src");

            ProjectRoot.Resolve([repository, gone, gone]).ShouldBe(new Dictionary<string, string>
            {
                [repository] = repository,
                [gone] = repository,
            });
        }
        finally
        {
            Directory.Delete(repository, recursive: true);
        }
    }
}
