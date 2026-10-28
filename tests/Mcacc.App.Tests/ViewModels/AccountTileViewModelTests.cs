using Mcacc.App.Localization;
using Mcacc.App.ViewModels;
using Mcacc.Core.Model;
using Mcacc.Core.Providers;
using Shouldly;
using Xunit;

namespace Mcacc.App.Tests.ViewModels;

public class AccountTileViewModelTests
{
    private static readonly Account Claude = new("privat", "Privat", "#6A9BCC", IsDefault: false);

    private static readonly Account Codex =
        new("codex-privat", "Codex Privat", "#5E8C8A", IsDefault: false, ProviderKind.Codex);

    /// <summary>
    /// MCACC was a Claude switcher, so an unmarked tile reads as Claude and only the accounts that need telling
    /// apart carry a word.
    /// </summary>
    [Fact]
    public void Only_an_account_of_another_service_carries_a_badge()
    {
        new AccountTileViewModel(Claude).ProviderBadge.ShouldBeNull();
        new AccountTileViewModel(Codex).ProviderBadge.ShouldBe(Texts.ProviderCodex);
    }

    [Fact]
    public void The_badge_follows_a_renamed_account()
    {
        var tile = new AccountTileViewModel(Claude);

        tile.Update(Codex);

        tile.ProviderBadge.ShouldBe(Texts.ProviderCodex);
    }

    [Fact]
    public void A_login_shows_its_address()
    {
        var tile = new AccountTileViewModel(Codex);

        tile.SetLogin(new AccountLoginInfo("ben@example.com", null, null));

        tile.Subtitle.ShouldBe("ben@example.com");
    }

    [Fact]
    public void No_login_says_so()
    {
        var tile = new AccountTileViewModel(Codex);

        tile.SetLogin(null);

        tile.Subtitle.ShouldBe(Texts.NotLoggedIn);
    }

    /// <summary>
    /// A Codex login with an API key has no address. Saying "not logged in" there would be wrong — the account is
    /// logged in, it just has nothing to show.
    /// </summary>
    [Fact]
    public void A_login_without_an_address_is_still_a_login()
    {
        var tile = new AccountTileViewModel(Codex);

        tile.SetLogin(new AccountLoginInfo(Email: null, null, null));

        tile.Subtitle.ShouldBe(Texts.LoggedInWithApiKey);
        tile.Subtitle.ShouldNotBe(Texts.NotLoggedIn);
    }

    [Fact]
    public void An_empty_address_counts_as_none()
    {
        var tile = new AccountTileViewModel(Codex);

        tile.SetLogin(new AccountLoginInfo("   ", null, null));

        tile.Subtitle.ShouldBe(Texts.LoggedInWithApiKey);
    }
}
