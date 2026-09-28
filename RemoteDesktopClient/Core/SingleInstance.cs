namespace RemoteDesktopClient.Core;

public static class SingleInstance
{
    private const string MutexName = "RemoteDesktopClient-SingleInstance-Mutex";
    private const string ActivateEventName = "RemoteDesktopClient-SingleInstance-ActivateEvent";

    private static Mutex? _mutex;
    private static EventWaitHandle? _activateEvent;

    public static bool TryAcquire()
    {
        _mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        if (createdNew)
        {
            _activateEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ActivateEventName);
            return true;
        }

        _mutex.Dispose();
        _mutex = null;

        try
        {
            using var activateEvent = EventWaitHandle.OpenExisting(ActivateEventName);
            activateEvent.Set();
        }
        catch (WaitHandleCannotBeOpenedException)
        {
        }
        return false;
    }

    public static void ListenForActivation(Action onActivateRequested)
    {
        if (_activateEvent is not { } activateEvent)
            throw new InvalidOperationException("SingleInstance.ListenForActivation was called without TryAcquire having returned true first.");

        var thread = new Thread(() =>
        {
            while (true)
            {
                activateEvent.WaitOne();
                onActivateRequested();
            }
        })
        {
            IsBackground = true,
            Name = "SingleInstance-ActivationListener",
        };
        thread.Start();
    }
}
