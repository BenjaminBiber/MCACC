namespace Mcacc.Core.Logging;

/// <summary>Never pass a token or a credential into any of these.</summary>
public interface ILog
{
    void Info(string message);

    void Warn(string message, Exception? exception = null);

    void Error(string message, Exception? exception = null);
}
