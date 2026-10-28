using System.Text.Json;

namespace Mcacc.Core.Codex;

/// <summary>
/// Reads the payload of a JWT that is already on this machine, in a file MCACC was told to trust. The signature is
/// deliberately <em>not</em> verified: MCACC is not authenticating anyone, it is displaying what the local file
/// says. Anyone able to forge the value could have edited the file outright.
/// </summary>
public static class JwtPayload
{
    /// <returns>Null when the value is not a three-part token whose middle part is base64url-encoded json.</returns>
    public static JsonDocument? Read(string? token)
    {
        if (token is null)
        {
            return null;
        }

        var parts = token.Split('.');
        if (parts.Length != 3)
        {
            return null;
        }

        try
        {
            return JsonDocument.Parse(Convert.FromBase64String(ToBase64(parts[1])));
        }
        catch (Exception e) when (e is FormatException or JsonException)
        {
            return null;
        }
    }

    /// <summary>Base64url to base64: the two swapped characters, and the padding the encoding leaves off.</summary>
    private static string ToBase64(string base64Url)
    {
        var padded = base64Url.Replace('-', '+').Replace('_', '/');
        return (padded.Length % 4) switch
        {
            2 => padded + "==",
            3 => padded + "=",
            _ => padded,
        };
    }
}
