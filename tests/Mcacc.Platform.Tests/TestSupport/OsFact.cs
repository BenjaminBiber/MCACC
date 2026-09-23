using System.Runtime.CompilerServices;
using Xunit;

namespace Mcacc.Platform.Tests.TestSupport;

/// <summary>A test that only runs on Windows; elsewhere it is reported as skipped.</summary>
public sealed class WindowsFactAttribute : FactAttribute
{
    public WindowsFactAttribute(
        [CallerFilePath] string? sourceFilePath = null,
        [CallerLineNumber] int sourceLineNumber = -1)
        : base(sourceFilePath, sourceLineNumber)
    {
        if (!OperatingSystem.IsWindows())
        {
            Skip = "Windows only.";
        }
    }
}

/// <summary>A test that only runs on macOS; elsewhere it is reported as skipped.</summary>
public sealed class MacFactAttribute : FactAttribute
{
    public MacFactAttribute(
        [CallerFilePath] string? sourceFilePath = null,
        [CallerLineNumber] int sourceLineNumber = -1)
        : base(sourceFilePath, sourceLineNumber)
    {
        if (!OperatingSystem.IsMacOS())
        {
            Skip = "macOS only.";
        }
    }
}
