using Mcacc.Core.ConfigCopy;
using Shouldly;
using Xunit;

namespace Mcacc.Core.Tests.ConfigCopy;

public class CopyItemsTests
{
    [Theory]
    [InlineData(CopyItem.ClaudeMd, "CLAUDE.md", false, CopyLocation.ClaudeDir)]
    [InlineData(CopyItem.SettingsJson, "settings.json", false, CopyLocation.ClaudeDir)]
    [InlineData(CopyItem.Agents, "agents", true, CopyLocation.ClaudeDir)]
    [InlineData(CopyItem.Skills, "skills", true, CopyLocation.ClaudeDir)]
    [InlineData(CopyItem.Commands, "commands", true, CopyLocation.ClaudeDir)]
    [InlineData(CopyItem.Scripts, "scripts", true, CopyLocation.ClaudeDir)]
    [InlineData(CopyItem.DesktopMcpConfig, "claude_desktop_config.json", false, CopyLocation.DesktopDir)]
    public void Info_describes_where_the_item_lives(
        CopyItem item, string relativePath, bool isDirectory, CopyLocation location)
    {
        CopyItems.Info(item).ShouldBe((relativePath, isDirectory, location));
    }

    [Fact]
    public void Every_item_has_its_own_path()
    {
        var items = Enum.GetValues<CopyItem>();

        items.Select(item => CopyItems.Info(item).RelativePath).Distinct().Count().ShouldBe(items.Length);
    }
}
