namespace Mcacc.Core.Processes;

public sealed class LaunchException : Exception
{
    public LaunchException(string message)
        : base(message)
    {
    }

    public LaunchException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
