namespace Mcacc.Platform;

/// <summary>
/// The macOS Dock icon, shown only while the window is: hidden, MCACC lives in the menu bar alone. Windows has no
/// Dock, and its taskbar button already follows the window.
/// </summary>
public interface IDockIcon
{
    void SetVisible(bool visible);
}

/// <summary>For the platforms without a Dock.</summary>
public sealed class NoDockIcon : IDockIcon
{
    public static readonly NoDockIcon Instance = new();

    public void SetVisible(bool visible)
    {
    }
}
