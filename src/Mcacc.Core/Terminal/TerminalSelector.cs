namespace Mcacc.Core.Terminal;

public static class TerminalSelector
{
    /// <returns>The configured terminal, else the platform default, else the first installed; null if none is installed.</returns>
    public static TerminalOption? Select(string? configuredId, IReadOnlyList<TerminalOption> installed, string platformDefaultId) =>
        installed.FirstOrDefault(option => configuredId is not null && option.Id == configuredId)
        ?? installed.FirstOrDefault(option => option.Id == platformDefaultId)
        ?? installed.FirstOrDefault();
}
