using System.Runtime.InteropServices;

namespace RemoteDesktopClient.Native.Windows;

public static class Win32ForegroundActivator
{
    private const int SwRestore = 9;

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

    public static void ForceToForeground(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero)
            return;

        if (IsIconic(hWnd))
            ShowWindow(hWnd, SwRestore);

        if (SetForegroundWindow(hWnd))
            return;

        var foregroundWindow = GetForegroundWindow();
        var foregroundThreadId = GetWindowThreadProcessId(foregroundWindow, out _);
        var currentThreadId = GetCurrentThreadId();
        if (foregroundThreadId == currentThreadId)
            return;

        if (AttachThreadInput(currentThreadId, foregroundThreadId, true))
        {
            try
            {
                SetForegroundWindow(hWnd);
            }
            finally
            {
                AttachThreadInput(currentThreadId, foregroundThreadId, false);
            }
        }
    }
}
