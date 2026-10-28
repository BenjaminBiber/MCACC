using Mcacc.Core.Desktop;
using Mcacc.Core.Model;

namespace Mcacc.Core.Abstractions;

public interface IDesktopInstanceDetector
{
    /// <returns>The running instances of that provider's desktop app; empty when it has none.</returns>
    Task<IReadOnlyList<DesktopInstance>> DetectAsync(ProviderDescriptor provider, CancellationToken ct);
}
