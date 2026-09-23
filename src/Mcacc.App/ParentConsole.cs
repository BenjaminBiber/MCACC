using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Mcacc.App;

/// <summary>
/// MCACC is a windowed app, so it starts without a console and <c>Console.WriteLine</c> would go nowhere. For
/// <c>--diagnose</c> it borrows the console of the shell that started it.
/// </summary>
internal static partial class ParentConsole
{
    private const int AttachParentProcess = -1;

    public static void Attach()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        if (AttachConsole(AttachParentProcess))
        {
            // Console caches the handles it had before attaching, so both streams are reopened.
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
            Console.SetError(new StreamWriter(Console.OpenStandardError()) { AutoFlush = true });
        }
    }

    [SupportedOSPlatform("windows")]
    [LibraryImport("kernel32.dll", EntryPoint = "AttachConsole", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AttachConsole(int processId);
}
