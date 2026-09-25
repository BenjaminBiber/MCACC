using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using Mcacc.Core.Logging;

namespace Mcacc.App;

/// <summary>
/// Keeps a second MCACC from starting: a named mutex on Windows, an exclusive lock file elsewhere. A failure to
/// check always counts as "only instance", because refusing to start would be worse than two windows. The second
/// start tells the first one over a named pipe to show its window.
/// </summary>
internal sealed class SingleInstance : IDisposable
{
    private const string MutexName = @"Local\MCACC";
    private const byte ActivateMessage = 1;
    private const int MaxListenFailures = 3;
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(1);

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

    /// <summary>
    /// Asks the running instance to show its window; it may be hidden in the tray, and a second start that did nothing
    /// would look broken.
    /// </summary>
    /// <returns>False when no instance answered in time.</returns>
    public static bool SignalExisting()
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName(), PipeDirection.Out, PipeOptions.CurrentUserOnly);
            client.Connect(ConnectTimeout);
            client.WriteByte(ActivateMessage);
            client.Flush();
            return true;
        }
        catch (Exception e) when (e is TimeoutException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    /// Waits for <see cref="SignalExisting"/> until disposed. <paramref name="onActivate"/> runs on a pool thread.
    /// </summary>
    public static IDisposable Listen(Action onActivate, ILog log)
    {
        var stop = new CancellationTokenSource();
        _ = Task.Run(() => ServeAsync(onActivate, log, stop.Token));
        return new Listener(stop);
    }

    /// <summary>
    /// One pipe per user and data folder, so a test run under its own <c>MCACC_HOME</c> does not reach the real app.
    /// Short, because on Unix the pipe is a socket whose path is limited to about a hundred characters.
    /// </summary>
    internal static string PipeName()
    {
        var key = Encoding.UTF8.GetBytes($"{Environment.UserName}|{McaccRoot()}");
        return $"MCACC-{Convert.ToHexString(SHA256.HashData(key), 0, 8)}";
    }

    private static async Task ServeAsync(Action onActivate, ILog log, CancellationToken ct)
    {
        var failures = 0;
        while (!ct.IsCancellationRequested && failures < MaxListenFailures)
        {
            try
            {
                await using var server = new NamedPipeServerStream(
                    PipeName(),
                    PipeDirection.In,
                    maxNumberOfServerInstances: 1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await server.WaitForConnectionAsync(ct).ConfigureAwait(false);
                var buffer = new byte[1];
                if (await server.ReadAsync(buffer, ct).ConfigureAwait(false) == 1 && buffer[0] == ActivateMessage)
                {
                    onActivate();
                }

                failures = 0;
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // A second start then just does nothing, as before; the app itself is unaffected.
                failures++;
                log.Warn("Listening for a second start failed.", e);
            }
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

    private sealed class Listener(CancellationTokenSource stop) : IDisposable
    {
        public void Dispose()
        {
            stop.Cancel();
            stop.Dispose();
        }
    }
}
