using System.Security;

namespace Mcacc.Platform.MacOS;

/// <summary>The LaunchAgent that starts MCACC hidden at login, only in a graphical session.</summary>
public static class LaunchAgentPlist
{
    public static string For(string label, string executablePath) => $"""
        <?xml version="1.0" encoding="UTF-8"?>
        <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
        <plist version="1.0">
        <dict>
          <key>Label</key>
          <string>{SecurityElement.Escape(label)}</string>
          <key>ProgramArguments</key>
          <array>
            <string>{SecurityElement.Escape(executablePath)}</string>
            <string>{LoginItem.HiddenSwitch}</string>
          </array>
          <key>RunAtLoad</key>
          <true/>
          <key>LimitLoadToSessionType</key>
          <string>Aqua</string>
        </dict>
        </plist>

        """.ReplaceLineEndings("\n");
}
