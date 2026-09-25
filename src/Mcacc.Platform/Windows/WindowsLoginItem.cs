using System.Runtime.Versioning;
using Microsoft.Win32;

namespace Mcacc.Platform.Windows;

/// <summary>
/// A value under the user's Run key. Task Manager's own switch for it lives in <c>StartupApproved</c>, which MCACC leaves
/// alone: turned off there, the entry stays off.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsLoginItem(string keyPath, string valueName) : ILoginItem
{
    public const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public WindowsLoginItem()
        : this(RunKey, "MCACC")
    {
    }

    public static string Command(string executablePath) => $"\"{executablePath}\" {LoginItem.HiddenSwitch}";

    public void Enable(string executablePath)
    {
        using var key = Registry.CurrentUser.CreateSubKey(keyPath, writable: true);
        key.SetValue(valueName, Command(executablePath), RegistryValueKind.String);
    }

    public void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(keyPath, writable: true);
        key?.DeleteValue(valueName, throwOnMissingValue: false);
    }
}
