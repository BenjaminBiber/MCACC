using System.Runtime.Versioning;
using Mcacc.Platform.Tests.TestSupport;
using Mcacc.Platform.Windows;
using Microsoft.Win32;
using Shouldly;
using Xunit;

namespace Mcacc.Platform.Tests.Windows;

[Trait("Category", "Integration")]
[SupportedOSPlatform("windows")]
public sealed class WindowsLoginItemTests : IDisposable
{
    // Never the real Run key: a test must not make MCACC start at the next login.
    private readonly string _key = $@"Software\MCACC-Tests-{Guid.NewGuid():N}";

    public void Dispose()
    {
        if (OperatingSystem.IsWindows())
        {
            Registry.CurrentUser.DeleteSubKeyTree(_key, throwOnMissingSubKey: false);
        }
    }

    private string? Value()
    {
        using var key = Registry.CurrentUser.OpenSubKey(_key);
        return key?.GetValue("MCACC") as string;
    }

    [WindowsFact]
    public void The_command_quotes_the_path_and_starts_hidden()
    {
        WindowsLoginItem.Command(@"C:\Program Files\MCACC\MCACC.exe").ShouldBe("\"C:\\Program Files\\MCACC\\MCACC.exe\" --hidden");
    }

    [WindowsFact]
    public void The_real_key_is_the_run_key_of_the_user()
    {
        WindowsLoginItem.RunKey.ShouldBe(@"Software\Microsoft\Windows\CurrentVersion\Run");
    }

    [WindowsFact]
    public void Enable_writes_the_value_and_disable_removes_it()
    {
        var item = new WindowsLoginItem(_key, "MCACC");

        item.Enable(@"C:\Tools\MCACC.exe");
        Value().ShouldBe("\"C:\\Tools\\MCACC.exe\" --hidden");

        item.Disable();
        Value().ShouldBeNull();
    }

    [WindowsFact]
    public void Disable_without_the_key_does_nothing()
    {
        Should.NotThrow(() => new WindowsLoginItem(_key, "MCACC").Disable());
    }
}
