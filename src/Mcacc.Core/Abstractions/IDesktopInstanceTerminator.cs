using Mcacc.Core.Desktop;

namespace Mcacc.Core.Abstractions;

public interface IDesktopInstanceTerminator
{
    /// <param name="grace">How long the instance may take to close by itself before it is killed.</param>
    Task<QuitResult> QuitAsync(DesktopInstance instance, TimeSpan grace, CancellationToken ct);
}
