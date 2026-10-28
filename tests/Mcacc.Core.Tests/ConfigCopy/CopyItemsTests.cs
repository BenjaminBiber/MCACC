using Mcacc.Core.ConfigCopy;
using Mcacc.Core.Model;
using Shouldly;
using Xunit;

namespace Mcacc.Core.Tests.ConfigCopy;

public class CopyItemsTests
{
    [Theory]
    [InlineData(CopyItem.ClaudeMd, "CLAUDE.md", false, CopyLocation.ConfigDir, PathRewrite.None)]
    [InlineData(CopyItem.SettingsJson, "settings.json", false, CopyLocation.ConfigDir, PathRewrite.Optional)]
    [InlineData(CopyItem.Agents, "agents", true, CopyLocation.ConfigDir, PathRewrite.None)]
    [InlineData(CopyItem.Skills, "skills", true, CopyLocation.ConfigDir, PathRewrite.None)]
    [InlineData(CopyItem.Commands, "commands", true, CopyLocation.ConfigDir, PathRewrite.None)]
    [InlineData(CopyItem.Scripts, "scripts", true, CopyLocation.ConfigDir, PathRewrite.None)]
    [InlineData(CopyItem.DesktopMcpConfig, "claude_desktop_config.json", false, CopyLocation.DesktopDir, PathRewrite.None)]
    [InlineData(CopyItem.CodexConfigToml, "config.toml", false, CopyLocation.ConfigDir, PathRewrite.Required)]
    [InlineData(CopyItem.AgentsMd, "AGENTS.md", false, CopyLocation.ConfigDir, PathRewrite.None)]
    [InlineData(CopyItem.CodexSkills, "skills", true, CopyLocation.ConfigDir, PathRewrite.None)]
    [InlineData(CopyItem.CodexPrompts, "prompts", true, CopyLocation.ConfigDir, PathRewrite.None)]
    public void Info_describes_where_the_item_lives_and_whether_it_is_rewritten(
        CopyItem item,
        string relativePath,
        bool isDirectory,
        CopyLocation location,
        PathRewrite rewrite)
    {
        CopyItems.Info(item).ShouldBe(new CopyItems.Entry(relativePath, isDirectory, location, rewrite));
    }

    [Fact]
    public void Every_item_is_described()
    {
        foreach (var item in Enum.GetValues<CopyItem>())
        {
            CopyItems.Info(item).RelativePath.ShouldNotBeNullOrWhiteSpace();
        }
    }

    /// <summary>
    /// Two providers may well keep different things under the same name — <c>skills</c> does — but within one
    /// provider a path must name exactly one item, or a copy would plan the same file twice.
    /// </summary>
    [Fact]
    public void Within_one_provider_every_item_has_its_own_path()
    {
        foreach (var provider in ProviderDescriptors.All)
        {
            var paths = provider.CopyItems
                .Select(item => CopyItems.Info(item))
                .Select(entry => (entry.RelativePath, entry.Location))
                .ToList();

            paths.Distinct().Count().ShouldBe(paths.Count, provider.DisplayName);
        }
    }

    /// <summary>Every item belongs to exactly one provider, so a source can never offer a foreign one.</summary>
    [Fact]
    public void Every_item_belongs_to_exactly_one_provider()
    {
        foreach (var item in Enum.GetValues<CopyItem>())
        {
            ProviderDescriptors.All.Count(p => p.CopyItems.Contains(item)).ShouldBe(1, item.ToString());
        }
    }

    /// <summary>A desktop item only makes sense for a provider whose desktop app MCACC can start per profile.</summary>
    [Fact]
    public void A_desktop_item_only_belongs_to_a_provider_that_has_a_desktop_app()
    {
        foreach (var provider in ProviderDescriptors.All)
        {
            if (provider.Desktop is null)
            {
                provider.CopyItems.ShouldAllBe(item => CopyItems.Info(item).Location == CopyLocation.ConfigDir);
            }
        }
    }
}
