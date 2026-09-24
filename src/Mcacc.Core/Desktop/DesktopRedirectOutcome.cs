namespace Mcacc.Core.Desktop;

public enum DesktopRedirectOutcome
{
    /// <summary>The account uses the app default profile, which a stray login would land in anyway.</summary>
    NotNeeded,

    /// <summary>No login came back through the browser; the window passed or Claude Desktop was closed.</summary>
    NotSeen,

    /// <summary>A login meant for another profile was caught and handed to the chosen one.</summary>
    Redirected,

    /// <summary>A login was caught, but handing it over failed; nobody is signed in by it.</summary>
    Failed,
}
