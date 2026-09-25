namespace Mcacc.Platform;

/// <summary>The entry that starts MCACC hidden in the tray when the user logs in.</summary>
public interface ILoginItem
{
    /// <summary>Creates the entry, or points it at <paramref name="executablePath"/> if it names another one.</summary>
    void Enable(string executablePath);

    /// <summary>Removes the entry; without one it does nothing.</summary>
    void Disable();
}

public static class LoginItem
{
    /// <summary>The argument of a start at login: MCACC comes up in the tray alone, without its window.</summary>
    public const string HiddenSwitch = "--hidden";
}

/// <summary>For tests and unknown platforms: there is no entry to keep.</summary>
public sealed class NoLoginItem : ILoginItem
{
    public static readonly NoLoginItem Instance = new();

    public void Enable(string executablePath)
    {
    }

    public void Disable()
    {
    }
}
