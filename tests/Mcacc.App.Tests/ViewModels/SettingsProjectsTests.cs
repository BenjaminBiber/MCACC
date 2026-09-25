using Mcacc.App.Localization;
using Mcacc.App.Tests.TestSupport;
using Mcacc.App.ViewModels;
using Mcacc.Core.Model;
using Mcacc.Core.Stats;
using Shouldly;
using Xunit;

namespace Mcacc.App.Tests.ViewModels;

/// <summary>The projects section of the settings page. The paths lead nowhere, so each counts as its own project.</summary>
public sealed class SettingsProjectsTests : IDisposable
{
    private static readonly Account Standard = Account.CreateDefault();
    private static readonly Account Privat = new("privat", "Privat", "#6A9BCC", IsDefault: false);
    private static readonly DateTimeOffset Hour = new(2026, 9, 25, 8, 0, 0, TimeSpan.Zero);

    private readonly TestServices _services = new(OsKind.Windows, Standard, Privat);

    public void Dispose() => _services.Dispose();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static TokenBucket Bucket(string project, long input) =>
        new(Hour, "claude-opus-4-5", project, "s1", new TokenCounts(input, 0, 0, 0, 0), 1);

    private IReadOnlyList<string>? Excluded => _services.Accounts.Config.Settings.ExcludedProjects;

    private async Task<SettingsViewModel> OpenAsync()
    {
        var page = new SettingsViewModel(_services.Services);
        await page.ActivateAsync(Ct);
        return page;
    }

    [Fact]
    public async Task The_projects_of_all_accounts_are_listed_by_their_tokens()
    {
        _services.Stats.ByAccount["standard"] = [Bucket("/nowhere/app", 100), Bucket("/nowhere/lab", 50)];
        _services.Stats.ByAccount["privat"] = [Bucket("/nowhere/lab/", 200), Bucket("", 999)];

        var page = await OpenAsync();

        page.Projects.Select(project => (project.Path, project.Tokens)).ShouldBe([("/nowhere/lab", 250L), ("/nowhere/app", 100L)]);
        page.Projects.ShouldAllBe(project => project.IsCounted);
        page.Projects[0].Name.ShouldBe("lab");
        page.ProjectsStatus.ShouldBeNull();
    }

    [Fact]
    public async Task Unticking_a_project_excludes_it()
    {
        _services.Stats.ByAccount["standard"] = [Bucket("/nowhere/app", 100), Bucket("/nowhere/lab", 50)];
        var page = await OpenAsync();

        page.Projects[1].IsCounted = false;

        Excluded.ShouldBe(["/nowhere/lab"]);
    }

    [Fact]
    public async Task Ticking_the_last_excluded_project_again_stores_none()
    {
        _services.Stats.ByAccount["standard"] = [Bucket("/nowhere/app", 100)];
        _services.Accounts.UpdateSettings(settings => settings with { ExcludedProjects = ["/nowhere/app"] });
        var page = await OpenAsync();
        page.Projects.ShouldHaveSingleItem().IsCounted.ShouldBeFalse();

        page.Projects[0].IsCounted = true;

        Excluded.ShouldBeNull();
    }

    [Fact]
    public async Task An_excluded_project_no_log_knows_stays_at_the_end()
    {
        _services.Stats.ByAccount["standard"] = [Bucket("/nowhere/app", 100)];
        _services.Accounts.UpdateSettings(settings => settings with { ExcludedProjects = ["/nowhere/gone"] });
        var page = await OpenAsync();

        page.Projects.Select(project => project.Path).ShouldBe(["/nowhere/app", "/nowhere/gone"]);
        page.Projects[1].IsCounted.ShouldBeFalse();
        page.Projects[1].TokensText.ShouldBe(Texts.SettingsProjectGone);

        page.Projects[0].IsCounted = false;

        Excluded.ShouldBe(["/nowhere/app", "/nowhere/gone"]);
    }

    [Fact]
    public async Task Without_any_project_the_page_says_so()
    {
        (await OpenAsync()).ProjectsStatus.ShouldBe(Texts.SettingsProjectsNone);
    }

    [Fact]
    public async Task Logs_that_cannot_be_read_show_a_failure()
    {
        _services.Stats.ThrowOnLoad = new IOException("locked");

        var page = await OpenAsync();

        page.Projects.ShouldBeEmpty();
        page.ProjectsStatus.ShouldBe(Texts.SettingsProjectsFailed);
    }

    [Fact]
    public async Task While_the_logs_are_read_the_page_says_so()
    {
        _services.Stats.Gate = new TaskCompletionSource();
        var page = new SettingsViewModel(_services.Services);

        var loading = page.ActivateAsync(Ct);

        page.ProjectsStatus.ShouldBe(Texts.SettingsProjectsLoading);
        _services.Stats.Gate.SetResult();
        await loading;
    }
}
