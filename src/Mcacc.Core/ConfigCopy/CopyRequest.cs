namespace Mcacc.Core.ConfigCopy;

public sealed record CopyRequest(string SourceAccountId, IReadOnlySet<CopyItem> Items, bool RewriteSettingsPaths);
