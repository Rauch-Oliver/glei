namespace TriasDeparture.Windows;

/// <summary>One instance per user session; a second start signals the first one.</summary>
public sealed class SingleInstance : IDisposable
{
    private readonly Mutex _mutex;
    private readonly EventWaitHandle _signal;
    private RegisteredWaitHandle? _registration;

    public bool IsFirst { get; }

    public SingleInstance(string name)
    {
        _mutex  = new Mutex(initiallyOwned: true, $@"Local\{name}", out var createdNew);
        _signal = new EventWaitHandle(false, EventResetMode.AutoReset, $@"Local\{name}.show");
        IsFirst = createdNew;
    }

    public void SignalFirst() => _signal.Set();

    /// <summary>Calls <paramref name="onSignal"/> (on a pool thread) whenever another instance starts.</summary>
    public void Listen(Action onSignal) =>
        _registration = ThreadPool.RegisterWaitForSingleObject(_signal, (_, _) => onSignal(), null, Timeout.Infinite, executeOnlyOnce: false);

    public void Dispose()
    {
        _registration?.Unregister(null);
        if (IsFirst) _mutex.ReleaseMutex();
        _mutex.Dispose();
        _signal.Dispose();
    }
}
