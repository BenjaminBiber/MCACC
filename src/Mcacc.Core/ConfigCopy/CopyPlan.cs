namespace Mcacc.Core.ConfigCopy;

public sealed record CopyPlan(IReadOnlyList<CopyOperation> Operations);
