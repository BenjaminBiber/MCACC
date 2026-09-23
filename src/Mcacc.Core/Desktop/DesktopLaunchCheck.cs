namespace Mcacc.Core.Desktop;

public abstract record DesktopLaunchCheck
{
    private DesktopLaunchCheck()
    {
    }

    public sealed record NotInstalled : DesktopLaunchCheck;

    public sealed record Ready : DesktopLaunchCheck;

    public sealed record RunningSameAccount : DesktopLaunchCheck;

    /// <param name="Conflicts">Every running instance that does not belong to the target account.</param>
    public sealed record Conflict(IReadOnlyList<DesktopConflict> Conflicts) : DesktopLaunchCheck;
}
