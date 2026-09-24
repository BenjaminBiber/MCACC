using System.Text.Json.Serialization;
using Mcacc.Core.Stats;
using Mcacc.Core.Usage;

namespace Mcacc.Core.Config;

/// <summary>Unindented json for the files that are line-based or large: the limit history and the stats index.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(UsageHistoryPoint))]
[JsonSerializable(typeof(StatsIndex))]
public sealed partial class McaccCompactJsonContext : JsonSerializerContext;
