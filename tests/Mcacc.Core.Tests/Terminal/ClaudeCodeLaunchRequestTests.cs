using Mcacc.Core.Tests.TestSupport;
using Shouldly;
using Xunit;

namespace Mcacc.Core.Tests.Terminal;

public class ClaudeCodeLaunchRequestTests
{
    [Fact]
    public void Title_names_claude_code_and_the_account()
    {
        LaunchRequests.WindowsProfile("Privat").Title.ShouldBe("Claude Code · Privat");
    }

    [Fact]
    public void Title_is_sanitized()
    {
        LaunchRequests.WindowsProfile("Ben's  \"Team\"; A&B").Title.ShouldBe("Claude Code · Bens Team AB");
    }

    [Fact]
    public void Title_is_cut_to_60_characters()
    {
        var title = LaunchRequests.WindowsProfile(new string('x', 80)).Title;

        title.ShouldBe("Claude Code · " + new string('x', 46));
    }
}
