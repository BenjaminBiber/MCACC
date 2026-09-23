namespace Mcacc.App;

/// <summary>
/// Keeps a second MCACC from starting: a named mutex on Windows, an exclusive lock file elsewhere. A failure to
/// check always counts as "only instance", because refusing to start would be worse than two windows.
/// </summary>
internal sealed class SingleInstance : IDisposable
{
    private const string MutexName = @"Local\MCACC";

    private readonly Mutex? _mutex;
    private readonly FileStream? _lockFile;

    private SingleInstance(bool isOnlyInstance, Mutex? mutex, FileStream? lockFile)
    {
        IsOnlyInstance = isOnlyInstance;
        _mutex = mutex;
        _lockFile = lockFile;
    }

    public bool IsOnlyInstance { get; }

    public static SingleInstance Acquire() => OperatingSystem.IsWindows() ? AcquireMutex() : AcquireLockFile();

    public void Dispose()
    {
        if (IsOnlyInstance)
        {
            _mutex?.ReleaseMutex();
        }

        _mutex?.Dispose();
        _lockFile?.Dispose();
    }

    private static SingleInstance AcquireMutex()
    {
        try
        {
            var mutex = new Mutex(initiallyOwned: false, MutexName);
            var acquired = mutex.WaitOne(TimeSpan.Zero, exitContext: false);
            return new SingleInstance(acquired, mutex, lockFile: null);
        }
        catch (Exception e) when (e is AbandonedMutexException or UnauthorizedAccessException or IOException)
        {
            return new SingleInstance(isOnlyInstance: true, mutex: null, lockFile: null);
        }
    }

    private static SingleInstance AcquireLockFile()
    {
        try
        {
            var root = McaccRoot();
            Directory.CreateDirectory(root);
            var stream = new FileStream(
                Path.Join(root, "mcacc.lock"),
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None);
            return new SingleInstance(isOnlyInstance: true, mutex: null, stream);
        }
        catch (IOException)
        {
            return new SingleInstance(isOnlyInstance: false, mutex: null, lockFile: null);
        }
        catch (Exception e) when (e is UnauthorizedAccessException or ArgumentException)
        {
            return new SingleInstance(isOnlyInstance: true, mutex: null, lockFile: null);
        }
    }

    /// <summary>The lock lives next to the config, so <c>MCACC_HOME</c> also separates the lock of a test run.</summary>
    private static string McaccRoot()
    {
        var overridden = Environment.GetEnvironmentVariable("MCACC_HOME");
        return string.IsNullOrWhiteSpace(overridden)
            ? Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".mcacc")
            : overridden;
    }
}
