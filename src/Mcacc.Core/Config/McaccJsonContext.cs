using System.Text.Json.Serialization;
using Mcacc.Core.Model;
using Mcacc.Core.Usage;

namespace Mcacc.Core.Config;

// Every serialized enum needs a CamelCaseEnumConverter entry; unlisted enums fall back to PascalCase strings.
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    NewLine = "\n",
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UseStringEnumConverter = true,
    Converters =
    [
        typeof(CamelCaseEnumConverter<WindowsTerminalShell>),
        typeof(CamelCaseEnumConverter<LaunchTarget>),
        typeof(ProviderKindConverter),
        typeof(CamelCaseEnumConverter<UsageSource>),
    ])]
[JsonSerializable(typeof(AppConfig))]
[JsonSerializable(typeof(Dictionary<string, UsageSnapshot>), TypeInfoPropertyName = "DictionaryStringUsageSnapshot")]
[JsonSerializable(typeof(Dictionary<string, AccountProfile>), TypeInfoPropertyName = "DictionaryStringAccountProfile")]
public sealed partial class McaccJsonContext : JsonSerializerContext;
