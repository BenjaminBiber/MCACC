using Mcacc.Core.Terminal;

namespace Mcacc.Core.Abstractions;

public interface ITerminalCatalog
{
    IReadOnlyList<TerminalOption> GetInstalled();

    /// <summary>The id preferred when the settings name no terminal; it need not be installed.</summary>
    string PlatformDefaultId { get; }
}
