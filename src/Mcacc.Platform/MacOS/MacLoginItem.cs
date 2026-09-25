namespace Mcacc.Platform.MacOS;

/// <summary>
/// A plist in <c>~/Library/LaunchAgents</c>; launchd reads it at the next login, so nothing is loaded now. macOS lists it
/// under System Settings → General → Login Items, where it can be switched off as well.
/// </summary>
public sealed class MacLoginItem(string launchAgentsDir, string label = MacLoginItem.DefaultLabel) : ILoginItem
{
    public const string DefaultLabel = "de.benjaminbiber.mcacc";

    private string PlistPath => Path.Join(launchAgentsDir, label + ".plist");

    public void Enable(string executablePath)
    {
        var content = LaunchAgentPlist.For(label, executablePath);
        if (File.Exists(PlistPath) && File.ReadAllText(PlistPath) == content)
        {
            return;
        }

        Directory.CreateDirectory(launchAgentsDir);
        var temp = PlistPath + ".tmp";
        File.WriteAllText(temp, content);
        File.Move(temp, PlistPath, overwrite: true);
    }

    public void Disable()
    {
        if (File.Exists(PlistPath))
        {
            File.Delete(PlistPath);
        }
    }
}
