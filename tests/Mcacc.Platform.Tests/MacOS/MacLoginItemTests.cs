using Mcacc.Platform.MacOS;
using Mcacc.Platform.Tests.TestSupport;
using Shouldly;
using Xunit;

namespace Mcacc.Platform.Tests.MacOS;

public sealed class MacLoginItemTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    private string AgentsDir => Path.Join(_temp.Path, "Library", "LaunchAgents");

    private string PlistPath => Path.Join(AgentsDir, "de.benjaminbiber.mcacc.plist");

    [Fact]
    public void The_plist_starts_the_binary_hidden_at_login()
    {
        LaunchAgentPlist.For("de.benjaminbiber.mcacc", "/Applications/MCACC.app/Contents/MacOS/MCACC").ShouldBe("""
            <?xml version="1.0" encoding="UTF-8"?>
            <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
            <plist version="1.0">
            <dict>
              <key>Label</key>
              <string>de.benjaminbiber.mcacc</string>
              <key>ProgramArguments</key>
              <array>
                <string>/Applications/MCACC.app/Contents/MacOS/MCACC</string>
                <string>--hidden</string>
              </array>
              <key>RunAtLoad</key>
              <true/>
              <key>LimitLoadToSessionType</key>
              <string>Aqua</string>
            </dict>
            </plist>

            """.ReplaceLineEndings("\n"));
    }

    [Fact]
    public void The_plist_escapes_the_path()
    {
        LaunchAgentPlist.For("x", "/Users/a&b/<MCACC>.app").ShouldContain("<string>/Users/a&amp;b/&lt;MCACC&gt;.app</string>");
    }

    [Fact]
    public void Enable_writes_the_plist_and_disable_removes_it()
    {
        var item = new MacLoginItem(AgentsDir);

        item.Enable("/Applications/MCACC.app/Contents/MacOS/MCACC");
        File.ReadAllText(PlistPath).ShouldBe(LaunchAgentPlist.For(MacLoginItem.DefaultLabel, "/Applications/MCACC.app/Contents/MacOS/MCACC"));

        item.Disable();
        File.Exists(PlistPath).ShouldBeFalse();
    }

    [Fact]
    public void Enable_again_follows_a_moved_app()
    {
        var item = new MacLoginItem(AgentsDir);

        item.Enable("/Applications/MCACC.app/Contents/MacOS/MCACC");
        item.Enable("/Users/ben/Apps/MCACC.app/Contents/MacOS/MCACC");

        File.ReadAllText(PlistPath).ShouldContain("<string>/Users/ben/Apps/MCACC.app/Contents/MacOS/MCACC</string>");
    }

    [Fact]
    public void Disable_without_a_plist_does_nothing()
    {
        Should.NotThrow(() => new MacLoginItem(AgentsDir).Disable());
    }
}
