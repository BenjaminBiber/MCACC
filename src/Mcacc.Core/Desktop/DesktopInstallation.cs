namespace Mcacc.Core.Desktop;

/// <param name="LaunchPath">The MSIX alias exe, the Squirrel exe, or the <c>.app</c> bundle.</param>
public sealed record DesktopInstallation(DesktopInstallKind Kind, string LaunchPath);
