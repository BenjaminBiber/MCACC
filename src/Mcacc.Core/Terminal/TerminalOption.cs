namespace Mcacc.Core.Terminal;

/// <param name="Id">One of <see cref="Mcacc.Core.Model.TerminalIds"/>.</param>
public sealed record TerminalOption(string Id, string DisplayName, string Path);
