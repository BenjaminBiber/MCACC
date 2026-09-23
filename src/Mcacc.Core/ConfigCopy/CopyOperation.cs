namespace Mcacc.Core.ConfigCopy;

public sealed record CopyOperation(CopyItem Item, string SourcePath, string TargetPath, bool IsDirectory, bool RewritePaths);
