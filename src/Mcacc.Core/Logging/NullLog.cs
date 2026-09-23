namespace Mcacc.Core.Logging;

/// <summary>Drops everything; the default for tests and for a run whose log directory is unusable.</summary>
public sealed class NullLog : ILog
{
    public static readonly NullLog Instance = new();

    public void Info(string message)
    {
    }

    public void Warn(string message, Exception? exception = null)
    {
    }

    public void Error(string message, Exception? exception = null)
    {
    }
}
