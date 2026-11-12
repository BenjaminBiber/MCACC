using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Mcacc.Core.ClaudeCode;

/// <summary>What <see cref="StatusLineSettings.Apply"/> did to one <c>settings.json</c>.</summary>
public enum StatusLineEdit
{
    Unchanged,
    Written,
    Removed,

    /// <summary>The file names a status line of its own, which MCACC never replaces.</summary>
    Foreign,

    /// <summary>The file is not a json object, so MCACC leaves it alone rather than lose what is in it.</summary>
    Unreadable,
}

/// <summary>
/// Puts MCACC's status line into a Claude Code <c>settings.json</c>, or takes it out again. MCACC's entry is the one
/// whose command carries <see cref="Switch"/>; every other key of the file stays as it is.
/// </summary>
public static class StatusLineSettings
{
    public const string FileName = "settings.json";

    /// <summary>The argument that starts MCACC as a status line instead of a window.</summary>
    public const string Switch = "--mcacc-statusline";

    private const string Key = "statusLine";

    private static readonly JsonDocumentOptions Lenient = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    private static readonly JsonWriterOptions Indented = new()
    {
        Indented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// The command for the status line. Claude Code runs it through Git Bash on Windows (PowerShell without one) and
    /// through sh elsewhere, so the path uses forward slashes and stays bare unless a space forces quotes.
    /// </summary>
    public static string Command(string executablePath)
    {
        var path = executablePath.Replace('\\', '/');
        return path.Contains(' ', StringComparison.Ordinal)
            ? $"'{path.Replace("'", "'\\''", StringComparison.Ordinal)}' {Switch}"
            : $"{path} {Switch}";
    }

    /// <param name="command">MCACC's command to put in, or null to take MCACC's entry out.</param>
    public static StatusLineEdit Apply(string settingsFile, string? command)
    {
        JsonObject root;
        if (!File.Exists(settingsFile))
        {
            if (command is null)
            {
                return StatusLineEdit.Unchanged;
            }

            root = [];
        }
        else if (Load(settingsFile) is { } loaded)
        {
            root = loaded;
        }
        else
        {
            return StatusLineEdit.Unreadable;
        }

        var current = root[Key];
        if (current is not null && !IsOwn(current))
        {
            return StatusLineEdit.Foreign;
        }

        if (command is null)
        {
            if (current is null)
            {
                return StatusLineEdit.Unchanged;
            }

            root.Remove(Key);
            Save(settingsFile, root);
            return StatusLineEdit.Removed;
        }

        if (current is JsonObject own && own["command"]?.GetValue<string>() == command)
        {
            return StatusLineEdit.Unchanged;
        }

        root[Key] = new JsonObject { ["type"] = "command", ["command"] = command };
        Save(settingsFile, root);
        return StatusLineEdit.Written;
    }

    private static bool IsOwn(JsonNode statusLine) =>
        statusLine is JsonObject entry
        && entry["command"] is JsonValue value
        && value.TryGetValue<string>(out var command)
        && command.Contains(Switch, StringComparison.Ordinal);

    private static JsonObject? Load(string file)
    {
        try
        {
            return JsonNode.Parse(File.ReadAllText(file), documentOptions: Lenient) as JsonObject;
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static void Save(string file, JsonObject root)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        var tempFile = file + ".mcacc.tmp";
        using (var stream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None))
        using (var writer = new Utf8JsonWriter(stream, Indented))
        {
            root.WriteTo(writer);
        }

        File.Move(tempFile, file, overwrite: true);
    }
}
