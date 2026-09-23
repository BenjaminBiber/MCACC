using Mcacc.Core.Config;
using Mcacc.Core.Model;
using Shouldly;
using Xunit;

namespace Mcacc.Core.Tests.Config;

public class ConfigValidatorTests
{
    private static readonly Account Standard = Account.CreateDefault();
    private static readonly Account Privat = new("privat", "Privat", "#6A9BCC", IsDefault: false);
    private static readonly Account Firma = new("firma", "Firma", "#7D9B76", IsDefault: false);

    private static AppConfig ConfigWith(IReadOnlyList<Account> accounts, AppSettings? settings = null) =>
        new(AppConfig.CurrentVersion, accounts, settings ?? new AppSettings());

    [Fact]
    public void A_valid_config_is_returned_unchanged_without_repairs()
    {
        var config = ConfigWith([Standard, Privat], new AppSettings(LastAccountId: "privat"));

        var (repaired, repairs) = ConfigValidator.Repair(config);

        repairs.ShouldBeEmpty();
        repaired.ShouldBeSameAs(config);
    }

    [Theory]
    [InlineData("Privat")]
    [InlineData("pri vat")]
    [InlineData("-privat")]
    [InlineData("privat-")]
    [InlineData("pri--vat")]
    [InlineData("privät")]
    [InlineData("../privat")]
    [InlineData("privat\n")]
    [InlineData("")]
    public void Drops_accounts_with_an_invalid_id(string id)
    {
        var config = ConfigWith([Standard, Privat with { Id = id }, Firma]);

        var (repaired, repairs) = ConfigValidator.Repair(config);

        repaired.Accounts.ShouldBe([Standard, Firma]);
        repairs.Count.ShouldBe(1);
    }

    [Theory]
    [InlineData("a")]
    [InlineData("kunde-mueller-2")]
    [InlineData("0")]
    public void Keeps_accounts_with_a_valid_slug_id(string id)
    {
        var account = Privat with { Id = id };

        var (repaired, repairs) = ConfigValidator.Repair(ConfigWith([Standard, account]));

        repaired.Accounts.ShouldBe([Standard, account]);
        repairs.ShouldBeEmpty();
    }

    [Fact]
    public void Drops_duplicate_ids_after_the_first()
    {
        var duplicate = Privat with { Name = "Privat 2", Color = "#9B7BA8" };

        var (repaired, repairs) = ConfigValidator.Repair(ConfigWith([Standard, Privat, Firma, duplicate]));

        repaired.Accounts.ShouldBe([Standard, Privat, Firma]);
        repairs.Count.ShouldBe(1);
    }

    [Fact]
    public void Inserts_the_default_account_first_when_none_is_default()
    {
        var (repaired, repairs) = ConfigValidator.Repair(ConfigWith([Privat, Firma]));

        repaired.Accounts.ShouldBe([Standard, Privat, Firma]);
        repairs.Count.ShouldBe(1);
    }

    [Fact]
    public void Promotes_a_non_default_standard_account_instead_of_inserting_a_second_one()
    {
        var standardAsProfile = new Account("standard", "Mein Standard", "#9B7BA8", IsDefault: false);

        var (repaired, repairs) = ConfigValidator.Repair(ConfigWith([Privat, standardAsProfile]));

        repaired.Accounts.ShouldBe([Privat, standardAsProfile with { IsDefault = true }]);
        repairs.Count.ShouldBe(1);
    }

    [Fact]
    public void Keeps_only_the_first_default()
    {
        var (repaired, repairs) = ConfigValidator.Repair(
            ConfigWith([Privat with { IsDefault = true }, Standard, Firma with { IsDefault = true }]));

        repaired.Accounts.ShouldBe([Privat with { IsDefault = true }, Standard with { IsDefault = false }, Firma]);
        repairs.Count.ShouldBe(2);
    }

    [Fact]
    public void Replaces_invalid_colors_with_the_next_free_palette_color()
    {
        var (repaired, repairs) = ConfigValidator.Repair(
            ConfigWith([Standard, Privat with { Color = "blue" }, Firma with { Color = "#6a9bcc0" }]));

        repaired.Accounts.Select(a => a.Color).ShouldBe(["#D97757", "#6A9BCC", "#7D9B76"]);
        repairs.Count.ShouldBe(2);
    }

    [Fact]
    public void Next_free_color_accounts_for_valid_colors_later_in_the_list()
    {
        var (repaired, _) = ConfigValidator.Repair(
            ConfigWith([Standard, Privat with { Color = "" }, Firma with { Color = "#6a9bcc" }]));

        repaired.Accounts.Select(a => a.Color).ShouldBe(["#D97757", "#7D9B76", "#6a9bcc"]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Replaces_empty_names_with_the_id(string name)
    {
        var (repaired, repairs) = ConfigValidator.Repair(ConfigWith([Standard, Privat with { Name = name }]));

        repaired.Accounts.ShouldBe([Standard, Privat with { Name = "privat" }]);
        repairs.Count.ShouldBe(1);
    }

    [Fact]
    public void Clears_an_unknown_last_account_id()
    {
        var settings = new AppSettings(LastAccountId: "gone", LastTarget: LaunchTarget.Code);

        var (repaired, repairs) = ConfigValidator.Repair(ConfigWith([Standard, Privat], settings));

        repaired.Settings.ShouldBe(settings with { LastAccountId = null });
        repairs.Count.ShouldBe(1);
    }

    [Fact]
    public void Clears_a_last_account_id_that_pointed_to_a_dropped_account()
    {
        var settings = new AppSettings(LastAccountId: "Privat");

        var (repaired, _) = ConfigValidator.Repair(ConfigWith([Standard, Privat with { Id = "Privat" }], settings));

        repaired.Settings.LastAccountId.ShouldBeNull();
    }

    [Fact]
    public void Keeps_other_config_parts_when_repairing()
    {
        var config = ConfigWith([Privat], new AppSettings(Terminal: TerminalIds.Cmd)) with { Version = 7 };

        var (repaired, _) = ConfigValidator.Repair(config);

        repaired.Version.ShouldBe(7);
        repaired.Settings.ShouldBe(config.Settings);
    }

    [Fact]
    public void Tolerates_missing_parts_from_hand_edited_json()
    {
        var config = new AppConfig(AppConfig.CurrentVersion, null!, null!);

        var (repaired, repairs) = ConfigValidator.Repair(config);

        repaired.Accounts.ShouldBe([Standard]);
        repaired.Settings.ShouldBe(new AppSettings());
        repairs.ShouldNotBeEmpty();
    }

    [Fact]
    public void Tolerates_null_account_entries_and_fields()
    {
        var config = ConfigWith([Standard, null!, new Account(null!, null!, null!, false), Privat with { Name = null!, Color = null! }]);

        var (repaired, _) = ConfigValidator.Repair(config);

        repaired.Accounts.ShouldBe([Standard, Privat with { Name = "privat", Color = "#6A9BCC" }]);
    }

    [Fact]
    public void Every_repair_is_described()
    {
        var config = ConfigWith(
            [Privat with { Name = "", Color = "x" }, Privat, Firma with { Id = "Bad Id" }],
            new AppSettings(LastAccountId: "gone"));

        var (_, repairs) = ConfigValidator.Repair(config);

        repairs.Count.ShouldBe(6);
        repairs.ShouldAllBe(r => !string.IsNullOrWhiteSpace(r));
    }
}
