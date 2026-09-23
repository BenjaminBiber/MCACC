using Mcacc.Core.Abstractions;

namespace Mcacc.Core.Desktop;

public sealed class DesktopInstanceDetector(IProcessSnapshotProvider provider, DesktopProcessRules rules) : IDesktopInstanceDetector
{
    public async Task<IReadOnlyList<DesktopInstance>> DetectAsync(CancellationToken ct)
    {
        var processes = await provider.GetClaudeProcessesAsync(ct).ConfigureAwait(false);
        return DesktopProcessClassifier.Classify(processes, rules);
    }
}
