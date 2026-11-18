using System.Net.Sockets;

namespace TriasDeparture.Linux;

/// <summary>
/// One instance per user: an exclusive lock file decides who is first, a Unix socket carries the
/// "show the popup" signal from a second start. (Named EventWaitHandles don't exist on Linux.)
/// </summary>
public sealed class SingleInstance : IDisposable
{
    private readonly FileStream? _lock;
    private readonly string _socketPath;
    private Socket? _listener;

    public bool IsFirst => _lock is not null;

    private SingleInstance(FileStream? lockFile, string socketPath)
    {
        _lock       = lockFile;
        _socketPath = socketPath;
    }

    public static SingleInstance Acquire(string name, string? directory = null)
    {
        directory ??= LinuxPaths.RuntimeDirectory;
        var lockPath   = Path.Combine(directory, $"{name}.lock");
        var socketPath = Path.Combine(directory, $"{name}.sock");
        try
        {
            // FileShare.None is an flock() on Unix: released by the kernel when the process dies, never stale
            var file = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            return new SingleInstance(file, socketPath);
        }
        catch (IOException)
        {
            return new SingleInstance(null, socketPath);
        }
    }

    public void SignalFirst()
    {
        try
        {
            using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            socket.Connect(new UnixDomainSocketEndPoint(_socketPath));
            socket.Send("show"u8);
        }
        catch (SocketException)
        {
            // The first instance is still starting up – nothing to show yet
        }
    }

    /// <summary>Calls <paramref name="onSignal"/> (on a pool thread) whenever another instance starts.</summary>
    public void Listen(Action onSignal)
    {
        if (!IsFirst) return;

        File.Delete(_socketPath); // left over from a crashed run; we hold the lock, so it is ours
        _listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        _listener.Bind(new UnixDomainSocketEndPoint(_socketPath));
        _listener.Listen(4);
        _ = AcceptLoopAsync(_listener, onSignal);
    }

    private static async Task AcceptLoopAsync(Socket listener, Action onSignal)
    {
        while (true)
        {
            try
            {
                using var client = await listener.AcceptAsync();
                onSignal();
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (SocketException)
            {
                // a client that hung up early – keep listening
            }
        }
    }

    public void Dispose()
    {
        if (_listener is not null)
        {
            _listener.Dispose();
            try { File.Delete(_socketPath); } catch (IOException) { }
        }
        _lock?.Dispose();
    }
}
