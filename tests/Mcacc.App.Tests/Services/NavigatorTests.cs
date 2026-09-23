using Mcacc.App.Services;
using Mcacc.App.ViewModels;
using Shouldly;
using Xunit;

namespace Mcacc.App.Tests.Services;

public class NavigatorTests
{
    private sealed class Page(string name) : PageViewModel
    {
        public string Name { get; } = name;
    }

    [Fact]
    public void A_new_navigator_shows_nothing()
    {
        var navigator = new Navigator();

        navigator.CurrentPage.ShouldBeNull();
        navigator.CanGoBack.ShouldBeFalse();
    }

    [Fact]
    public void Navigating_shows_the_page_and_allows_going_back()
    {
        var navigator = new Navigator();
        var first = new Page("first");
        var second = new Page("second");

        navigator.Reset(first);
        navigator.CanGoBack.ShouldBeFalse();

        navigator.NavigateTo(second);
        navigator.CurrentPage.ShouldBe(second);
        navigator.CanGoBack.ShouldBeTrue();

        navigator.Back();
        navigator.CurrentPage.ShouldBe(first);
        navigator.CanGoBack.ShouldBeFalse();
    }

    [Fact]
    public void Going_back_without_history_keeps_the_page()
    {
        var navigator = new Navigator();
        var page = new Page("only");
        navigator.Reset(page);

        navigator.Back();

        navigator.CurrentPage.ShouldBe(page);
    }

    [Fact]
    public void Reset_drops_the_history()
    {
        var navigator = new Navigator();
        navigator.Reset(new Page("first"));
        navigator.NavigateTo(new Page("second"));

        navigator.Reset(new Page("third"));

        navigator.CanGoBack.ShouldBeFalse();
    }

    [Fact]
    public void Every_change_is_announced()
    {
        var navigator = new Navigator();
        var seen = new List<PageViewModel>();
        navigator.Navigated += seen.Add;
        var first = new Page("first");
        var second = new Page("second");

        navigator.Reset(first);
        navigator.NavigateTo(second);
        navigator.Back();

        seen.ShouldBe([first, second, first]);
    }
}
