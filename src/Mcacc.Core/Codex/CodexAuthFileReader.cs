namespace Mcacc.Core.Codex;

/// <summary>Reads <c>auth.json</c> from disk. Codex keeps it open while it runs, so it is shared.</summary>
public static class CodexAuthFileReader
{
    public enum Outcome
    {
        Found,
        Missing,
        Unreadable,
    }

    public static (Outcome Result, CodexAuth? Auth) Read(string path)
    {
        string json;
        try
        {
            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            json = reader.ReadToEnd();
        }
        catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException)
        {
            return (Outcome.Missing, null);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return (Outcome.Unreadable, null);
        }

        return CodexAuthParser.Parse(json) is { } auth ? (Outcome.Found, auth) : (Outcome.Unreadable, null);
    }
}
