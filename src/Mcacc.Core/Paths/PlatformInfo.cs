using Mcacc.Core.Model;

namespace Mcacc.Core.Paths;

public sealed record PlatformInfo(OsKind Os, string HomeDir, string RoamingAppData, string LocalAppData)
{
    public PathStyle PathStyle => Os == OsKind.Windows ? PathStyle.Windows : PathStyle.Posix;

    public static PlatformInfo Current() => new(
        CurrentOs(),
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));

    private static OsKind CurrentOs()
    {
        if (OperatingSystem.IsWindows())
        {
            return OsKind.Windows;
        }

        return OperatingSystem.IsMacOS() ? OsKind.MacOS : OsKind.Linux;
    }
}
