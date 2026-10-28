using Mcacc.Core.Accounts;
using Mcacc.Core.Model;
using Shouldly;
using Xunit;

namespace Mcacc.Core.Tests.Model;

public class ProviderDescriptorsTests
{
    [Fact]
    public void Claude_is_the_zero_value_so_a_config_without_the_field_reads_as_Claude()
    {
        ((int)ProviderKind.Claude).ShouldBe(0);
        default(ProviderKind).ShouldBe(ProviderKind.Claude);
    }

    [Fact]
    public void The_Claude_descriptor_carries_what_the_app_knows_today()
    {
        var claude = ProviderDescriptors.Claude;

        claude.Kind.ShouldBe(ProviderKind.Claude);
        claude.ProfileSubfolder.ShouldBe("claude");
        claude.DesktopSubfolder.ShouldBe("desktop");
        claude.Cli.Command.ShouldBe("claude");
        claude.Cli.ConfigDirVariable.ShouldBe("CLAUDE_CONFIG_DIR");
        claude.Cli.DefaultHomeFolder.ShouldBe(".claude");
        claude.Defaults.AccountId.ShouldBe(Account.DefaultId);
        claude.Defaults.AccountName.ShouldBe(Account.DefaultName);
        claude.Defaults.Color.ShouldBe(Account.DefaultColor);

        var desktop = claude.Desktop.ShouldNotBeNull();
        desktop.MsixPackageFamilyName.ShouldBe("Claude_pzs8sxrjxfjjc");
        desktop.MacBundleId.ShouldBe("com.anthropic.claudefordesktop");
        desktop.DeepLinkScheme.ShouldBe("claude://");
        desktop.DataFolderName.ShouldBe("Claude");
        desktop.WindowsPathDiscriminator.ShouldBe(@"\WindowsApps\Claude_");
    }

    [Fact]
    public void The_Codex_descriptor_carries_the_values_the_spike_verified()
    {
        var codex = ProviderDescriptors.Codex;

        codex.Kind.ShouldBe(ProviderKind.Codex);
        codex.ProfileSubfolder.ShouldBe("codex");
        codex.DesktopSubfolder.ShouldBe("codex-desktop");
        codex.Cli.Command.ShouldBe("codex");
        codex.Cli.ConfigDirVariable.ShouldBe("CODEX_HOME");
        codex.Cli.DefaultHomeFolder.ShouldBe(".codex");

        var desktop = codex.Desktop.ShouldNotBeNull();
        desktop.MsixPackageFamilyName.ShouldBe("OpenAI.Codex_2p2nqsd0c76g0");
        desktop.MacBundleId.ShouldBe("com.openai.codex");
        desktop.DeepLinkScheme.ShouldBe("codex://");
        desktop.DataFolderName.ShouldBe("Codex");
        desktop.WindowsPathDiscriminator.ShouldBe(@"\WindowsApps\OpenAI.Codex_");
    }

    /// <summary>
    /// The desktop app is called ChatGPT; naming it Codex would send the user looking for an entry that is not there.
    /// </summary>
    [Fact]
    public void The_Codex_desktop_app_is_named_ChatGPT()
    {
        ProviderDescriptors.Codex.DesktopDisplayName.ShouldBe("ChatGPT");
        ProviderDescriptors.Claude.DesktopDisplayName.ShouldBe("Claude Desktop");
    }

    /// <summary>
    /// Claude Desktop takes the profile from <c>--user-data-dir</c> alone; the Codex app wants its Electron profile
    /// named by an environment variable as well, and its own second-profile launcher sets both.
    /// </summary>
    [Fact]
    public void Only_the_Codex_desktop_app_needs_a_user_data_dir_variable()
    {
        ProviderDescriptors.Claude.Desktop.ShouldNotBeNull().UserDataDirVariable.ShouldBeNull();
        ProviderDescriptors.Codex.Desktop.ShouldNotBeNull().UserDataDirVariable.ShouldBe("CODEX_ELECTRON_USER_DATA_PATH");
    }

    /// <summary>
    /// Both desktop apps run on Chromium, so the process name alone is ambiguous — the separate ChatGPT app is also
    /// called ChatGPT.exe, and the Claude Code CLI is also called claude(.exe).
    /// </summary>
    [Fact]
    public void Every_desktop_app_is_told_apart_by_its_package_path_not_by_its_process_name()
    {
        foreach (var provider in ProviderDescriptors.All)
        {
            var desktop = provider.Desktop;
            if (desktop is null)
            {
                continue;
            }

            desktop.WindowsPathDiscriminator.ShouldStartWith(@"\WindowsApps\");
            desktop.WindowsPathDiscriminator.ShouldContain(desktop.MsixPackageFamilyName.Split('_')[0]);
        }
    }

    [Fact]
    public void All_covers_every_provider_kind()
    {
        ProviderDescriptors.All.Select(p => p.Kind).ShouldBe(Enum.GetValues<ProviderKind>(), ignoreOrder: true);
    }

    [Theory]
    [InlineData(ProviderKind.Claude)]
    [InlineData(ProviderKind.Codex)]
    public void For_returns_the_descriptor_of_the_kind(ProviderKind kind)
    {
        ProviderDescriptors.For(kind).Kind.ShouldBe(kind);
    }

    [Fact]
    public void For_rejects_a_kind_that_is_not_a_provider()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => ProviderDescriptors.For((ProviderKind)42));
    }

    [Fact]
    public void For_an_account_follows_its_provider()
    {
        var codex = new Account("privat", "Privat", "#6A9BCC", IsDefault: false, ProviderKind.Codex);

        ProviderDescriptors.For(codex).ShouldBeSameAs(ProviderDescriptors.Codex);
        ProviderDescriptors.For(Account.CreateDefault()).ShouldBeSameAs(ProviderDescriptors.Claude);
    }

    /// <summary>Two providers sharing a subfolder would overwrite each other's configuration.</summary>
    [Fact]
    public void Every_provider_has_its_own_subfolders_and_default_account_id()
    {
        var count = ProviderDescriptors.All.Count;
        ProviderDescriptors.All.Select(p => p.ProfileSubfolder).Distinct().Count().ShouldBe(count);
        ProviderDescriptors.All.Select(p => p.DesktopSubfolder).Distinct().Count().ShouldBe(count);
        ProviderDescriptors.All.Select(p => p.Defaults.AccountId).Distinct().Count().ShouldBe(count);
    }

    /// <summary>A profile subfolder also must not collide with a desktop one, since both live under the profile.</summary>
    [Fact]
    public void No_profile_subfolder_is_also_a_desktop_subfolder()
    {
        var profiles = ProviderDescriptors.All.Select(p => p.ProfileSubfolder).ToHashSet(StringComparer.OrdinalIgnoreCase);

        ProviderDescriptors.All.ShouldAllBe(p => !profiles.Contains(p.DesktopSubfolder));
    }

    [Fact]
    public void Every_default_account_has_a_valid_color_and_a_valid_id()
    {
        foreach (var provider in ProviderDescriptors.All)
        {
            AccountPalette.IsValidColor(provider.Defaults.Color).ShouldBeTrue(provider.DisplayName);
            AccountId.IsValid(provider.Defaults.AccountId).ShouldBeTrue(provider.DisplayName);
        }
    }

    [Fact]
    public void Every_descriptor_is_filled_in()
    {
        foreach (var provider in ProviderDescriptors.All)
        {
            provider.DisplayName.ShouldNotBeNullOrWhiteSpace();
            provider.CliDisplayName.ShouldNotBeNullOrWhiteSpace();
            provider.DesktopDisplayName.ShouldNotBeNullOrWhiteSpace();
            provider.ProfileSubfolder.ShouldNotBeNullOrWhiteSpace();
            provider.DesktopSubfolder.ShouldNotBeNullOrWhiteSpace();
            provider.Cli.Command.ShouldNotBeNullOrWhiteSpace();
            provider.Cli.ConfigDirVariable.ShouldNotBeNullOrWhiteSpace();
            provider.Cli.DefaultHomeFolder.ShouldNotBeNullOrWhiteSpace();
        }
    }
}
