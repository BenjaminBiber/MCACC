using Mcacc.Core.Desktop;

namespace Mcacc.Core.Abstractions;

public interface IDesktopInstanceDetector
{
    Task<IReadOnlyList<DesktopInstance>> DetectAsync(CancellationToken ct);
}
