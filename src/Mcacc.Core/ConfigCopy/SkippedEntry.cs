namespace Mcacc.Core.ConfigCopy;

/// <param name="RelativePath">
/// Relative to the account's Claude or Desktop directory, joined with the OS separator, e.g. <c>skills/a/SKILL.md</c>.
/// </param>
public sealed record SkippedEntry(string RelativePath, CopySkipReason Reason);
