using System.Runtime.InteropServices;
using RemoteDesktopClient.Core.Control;

namespace RemoteDesktopClient.Native.Windows;

public sealed class Win32ClipboardWatcher : IClipboardWatcher
{
    private const uint WM_CLIPBOARDUPDATE = 0x031D;
    private const int GWLP_WNDPROC = -4;

    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    private readonly WndProcDelegate _wndProc;

    private IntPtr _windowHandle;
    private IntPtr _originalWndProc;

    public event Action? ClipboardChanged;

    public Win32ClipboardWatcher()
    {
        _wndProc = WndProc;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool AddClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", EntryPoint = "CallWindowProcW")]
    private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    public void Attach(IntPtr windowHandle)
    {
        if (_windowHandle != IntPtr.Zero)
            return;

        _windowHandle = windowHandle;
        var newWndProcPointer = Marshal.GetFunctionPointerForDelegate(_wndProc);
        _originalWndProc = SetWindowLongPtr(_windowHandle, GWLP_WNDPROC, newWndProcPointer);
        AddClipboardFormatListener(_windowHandle);
    }

    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == WM_CLIPBOARDUPDATE)
            ClipboardChanged?.Invoke();
        return CallWindowProc(_originalWndProc, hWnd, msg, wParam, lParam);
    }

    public void Dispose()
    {
        if (_windowHandle == IntPtr.Zero)
            return;

        RemoveClipboardFormatListener(_windowHandle);
        SetWindowLongPtr(_windowHandle, GWLP_WNDPROC, _originalWndProc);
        _windowHandle = IntPtr.Zero;
    }
}
