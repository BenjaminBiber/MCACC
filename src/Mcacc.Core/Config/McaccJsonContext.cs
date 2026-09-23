using System.Text.Json.Serialization;
using Mcacc.Core.Model;

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
    ])]
[JsonSerializable(typeof(AppConfig))]
public sealed partial class McaccJsonContext : JsonSerializerContext;
