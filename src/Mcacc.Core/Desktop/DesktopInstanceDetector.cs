using Mcacc.Core.Abstractions;
using Mcacc.Core.Model;
using Mcacc.Core.Paths;

namespace Mcacc.Core.Desktop;

public sealed class DesktopInstanceDetector(IProcessSnapshotProvider provider, PlatformInfo info)
    : IDesktopInstanceDetector
{
    public async Task<IReadOnlyList<DesktopInstance>> DetectAsync(ProviderDescriptor descriptor, CancellationToken ct)
    {
        if (descriptor.Desktop is null)
        {
            return [];
        }

        var processes = await provider.GetProcessesAsync(descriptor, ct).ConfigureAwait(false);
        return DesktopProcessClassifier.Classify(processes, DesktopProcessRules.For(info.Os, descriptor));
    }
}
