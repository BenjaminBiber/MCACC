using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mcacc.Core.Config;

internal sealed class CamelCaseEnumConverter<TEnum>() : JsonStringEnumConverter<TEnum>(JsonNamingPolicy.CamelCase)
    where TEnum : struct, Enum;
