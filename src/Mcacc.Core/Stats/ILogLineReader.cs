namespace Mcacc.Core.Stats;

/// <summary>
/// What a resumed scan has to put back before it reads the first new line. Claude logs every response with its own
/// model and working directory; Codex writes them once per turn and the usage lines after that say nothing about
/// either, so a reader that starts mid-file would attribute them to nothing.
/// </summary>
public sealed record LogReaderState(string? Model, string? Project)
{
    public static readonly LogReaderState None = new(null, null);
}

/// <summary>
/// Reads the usage out of one log line. One instance per file, because some formats carry state across lines.
/// </summary>
public interface ILogLineReader
{
    /// <summary>What the reader would have to be told to continue in the middle of this file.</summary>
    LogReaderState State { get; set; }

    /// <returns>False when the line could not be read; a readable line without usage yields true and a null usage.</returns>
    bool TryRead(ReadOnlyMemory<byte> line, out LogUsage? usage);
}
