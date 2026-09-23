using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Mcacc.Platform.MacOS;

[SupportedOSPlatform("macos")]
internal static partial class LibC
{
    public const int Sigterm = 15;
    public const int Sigkill = 9;

    /// <summary>errno: no such process.</summary>
    public const int Esrch = 3;

    /// <summary>Signal 0 only checks whether the process exists.</summary>
    [LibraryImport("libc", EntryPoint = "kill", SetLastError = true)]
    internal static partial int Kill(int pid, int sig);
}
