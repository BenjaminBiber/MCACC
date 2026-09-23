namespace Mcacc.Core.ConfigCopy;

/// <param name="Copied">Items that were copied; a merged directory counts even if some entries inside were skipped.</param>
public sealed record CopyReport(IReadOnlyList<CopyItem> Copied, IReadOnlyList<SkippedEntry> Skipped);
