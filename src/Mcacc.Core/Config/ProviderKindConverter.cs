using System.Text.Json;
using System.Text.Json.Serialization;
using Mcacc.Core.Model;

namespace Mcacc.Core.Config;

/// <summary>
/// Writes camelCase like <see cref="CamelCaseEnumConverter{TEnum}"/>, but reads anything it does not recognise as
/// <see cref="ProviderKind.Claude"/> instead of throwing. A config naming a provider this version does not know
/// must still load: a throw would make <see cref="ConfigStore"/> quarantine the whole file as corrupt rather than
/// open it read-only, and the user would lose their account list over one unknown word.
/// </summary>
internal sealed class ProviderKindConverter : JsonConverter<ProviderKind>
{
    public override ProviderKind Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // Enum.TryParse also accepts a numeric string, which yields a value outside the enum — hence IsDefined.
        if (reader.TokenType == JsonTokenType.String
            && Enum.TryParse<ProviderKind>(reader.GetString(), ignoreCase: true, out var kind)
            && Enum.IsDefined(kind))
        {
            return kind;
        }

        if (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray)
        {
            reader.Skip();
        }

        return ProviderKind.Claude;
    }

    public override void Write(Utf8JsonWriter writer, ProviderKind value, JsonSerializerOptions options) =>
        writer.WriteStringValue(JsonNamingPolicy.CamelCase.ConvertName(value.ToString()));
}
