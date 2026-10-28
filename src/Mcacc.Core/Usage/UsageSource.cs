namespace Mcacc.Core.Usage;

/// <summary>Where a snapshot's numbers came from. A local one is only as current as the last session.</summary>
public enum UsageSource
{
    Live,
    LocalFile,
}
