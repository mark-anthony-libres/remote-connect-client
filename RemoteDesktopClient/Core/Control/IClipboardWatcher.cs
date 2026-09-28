namespace RemoteDesktopClient.Core.Control;

public interface IClipboardWatcher : IDisposable
{
    event Action? ClipboardChanged;

    void Attach(IntPtr windowHandle);
}

public sealed class NullClipboardWatcher : IClipboardWatcher
{
    public event Action? ClipboardChanged { add { } remove { } }
    public void Attach(IntPtr windowHandle) { }
    public void Dispose() { }
}
