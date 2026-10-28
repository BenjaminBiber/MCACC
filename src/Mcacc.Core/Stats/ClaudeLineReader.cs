namespace Mcacc.Core.Stats;

/// <summary>
/// Claude Code names the model and the working directory on every response, so nothing is carried between lines and
/// a resumed scan needs no state.
/// </summary>
public sealed class ClaudeLineReader : ILogLineReader
{
    public LogReaderState State { get; set; } = LogReaderState.None;

    public bool TryRead(ReadOnlyMemory<byte> line, out LogUsage? usage) => LogLineParser.TryParse(line, out usage);
}
