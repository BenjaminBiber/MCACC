using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mcacc.Core.Model;

public sealed record AppConfig(int Version, IReadOnlyList<Account> Accounts, AppSettings Settings)
{
    public const int CurrentVersion = 1;

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; init; }
}
