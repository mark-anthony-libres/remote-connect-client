using System.Runtime.InteropServices;
using System.Text;
using RemoteDesktopClient.Core.Control;

namespace RemoteDesktopClient.Native.Windows;

public sealed class Win32Clipboard : IRemoteClipboard
{
    private const uint CfUnicodeText = 13;
    private const uint GMemMoveable = 0x0002;

    private static readonly uint PngFormat = RegisterClipboardFormat("PNG");

    private const uint CfHDrop = 15;
    private const int DropEffectCopy = 1;
    private static readonly uint PreferredDropEffectFormat = RegisterClipboardFormat("Preferred DropEffect");

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "DragQueryFileW")]
    private static extern uint DragQueryFile(IntPtr hDrop, uint iFile, StringBuilder? lpszFile, uint cch);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool OpenClipboard(IntPtr hWndNewOwner);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool EmptyClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool IsClipboardFormatAvailable(uint format);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetClipboardData(uint uFormat);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RegisterClipboardFormat(string lpszFormat);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalLock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalUnlock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern UIntPtr GlobalSize(IntPtr hMem);

    public bool TryGetText(out string text)
    {
        string? result = null;
        WithClipboardOpen(() =>
        {
            if (!IsClipboardFormatAvailable(CfUnicodeText))
                return false;
            var handle = GetClipboardData(CfUnicodeText);
            if (handle == IntPtr.Zero)
                return false;

            var pointer = GlobalLock(handle);
            if (pointer == IntPtr.Zero)
                return false;
            try
            {
                result = Marshal.PtrToStringUni(pointer) ?? "";
                return true;
            }
            finally
            {
                GlobalUnlock(handle);
            }
        }, out var got);

        text = result ?? "";
        return got && result is not null;
    }

    public void SetText(string text)
    {
        var bytes = Encoding.Unicode.GetBytes(text + '\0');
        var handle = GlobalAlloc(GMemMoveable, (UIntPtr)bytes.Length);
        if (handle == IntPtr.Zero)
            return;

        var pointer = GlobalLock(handle);
        if (pointer == IntPtr.Zero)
            return;
        Marshal.Copy(bytes, 0, pointer, bytes.Length);
        GlobalUnlock(handle);

        WithClipboardOpen(() =>
        {
            EmptyClipboard();
            return SetClipboardData(CfUnicodeText, handle) != IntPtr.Zero;
        }, out _);
    }

    public bool TryGetImagePng(out byte[] pngBytes)
    {
        pngBytes = [];
        byte[]? result = null;
        WithClipboardOpen(() =>
        {
            if (!IsClipboardFormatAvailable(PngFormat))
                return false;
            var handle = GetClipboardData(PngFormat);
            if (handle == IntPtr.Zero)
                return false;

            var pointer = GlobalLock(handle);
            if (pointer == IntPtr.Zero)
                return false;
            try
            {
                var size = (int)GlobalSize(handle);
                var buffer = new byte[size];
                Marshal.Copy(pointer, buffer, 0, size);
                result = buffer;
                return true;
            }
            finally
            {
                GlobalUnlock(handle);
            }
        }, out var got);

        if (got && result is { } bytes)
        {
            pngBytes = bytes;
            return true;
        }
        return false;
    }

    public void SetImagePng(byte[] pngBytes)
    {
        var handle = GlobalAlloc(GMemMoveable, (UIntPtr)pngBytes.Length);
        if (handle == IntPtr.Zero)
            return;

        var pointer = GlobalLock(handle);
        if (pointer == IntPtr.Zero)
            return;
        Marshal.Copy(pngBytes, 0, pointer, pngBytes.Length);
        GlobalUnlock(handle);

        WithClipboardOpen(() =>
        {
            EmptyClipboard();
            return SetClipboardData(PngFormat, handle) != IntPtr.Zero;
        }, out _);
    }

    public bool TryGetFilePaths(out IReadOnlyList<string> paths)
    {
        paths = [];
        List<string>? result = null;
        WithClipboardOpen(() =>
        {
            if (!IsClipboardFormatAvailable(CfHDrop))
                return false;
            var hDrop = GetClipboardData(CfHDrop);
            if (hDrop == IntPtr.Zero)
                return false;

            var count = DragQueryFile(hDrop, 0xFFFFFFFF, null, 0);
            var files = new List<string>((int)count);
            for (uint index = 0; index < count; index++)
            {
                var length = DragQueryFile(hDrop, index, null, 0);
                if (length == 0)
                    continue;
                var buffer = new StringBuilder((int)length + 1);
                if (DragQueryFile(hDrop, index, buffer, (uint)buffer.Capacity) > 0)
                    files.Add(buffer.ToString());
            }
            result = files;
            return files.Count > 0;
        }, out var got);

        if (got && result is { Count: > 0 } found)
        {
            paths = found;
            return true;
        }
        return false;
    }

    public void SetFilePaths(IReadOnlyList<string> paths)
    {
        if (paths.Count == 0)
            return;

        const int dropFilesHeaderSize = 20;
        var fileList = Encoding.Unicode.GetBytes(string.Join('\0', paths) + "\0\0");
        var dropFiles = new byte[dropFilesHeaderSize + fileList.Length];
        BitConverter.GetBytes(dropFilesHeaderSize).CopyTo(dropFiles, 0);
        BitConverter.GetBytes(1).CopyTo(dropFiles, 16);
        fileList.CopyTo(dropFiles, dropFilesHeaderSize);

        var hDrop = CopyToGlobal(dropFiles);
        var hDropEffect = CopyToGlobal(BitConverter.GetBytes(DropEffectCopy));
        if (hDrop == IntPtr.Zero || hDropEffect == IntPtr.Zero)
            return;

        WithClipboardOpen(() =>
        {
            EmptyClipboard();
            var ok = SetClipboardData(CfHDrop, hDrop) != IntPtr.Zero;
            SetClipboardData(PreferredDropEffectFormat, hDropEffect);
            return ok;
        }, out _);
    }

    private static IntPtr CopyToGlobal(byte[] bytes)
    {
        var handle = GlobalAlloc(GMemMoveable, (UIntPtr)bytes.Length);
        if (handle == IntPtr.Zero)
            return IntPtr.Zero;

        var pointer = GlobalLock(handle);
        if (pointer == IntPtr.Zero)
            return IntPtr.Zero;
        Marshal.Copy(bytes, 0, pointer, bytes.Length);
        GlobalUnlock(handle);
        return handle;
    }

    private static bool WithClipboardOpen(Func<bool> action, out bool result)
    {
        result = false;
        for (var attempt = 0; attempt < 5; attempt++)
        {
            if (OpenClipboard(IntPtr.Zero))
            {
                try
                {
                    result = action();
                }
                finally
                {
                    CloseClipboard();
                }
                return true;
            }
            Thread.Sleep(30);
        }
        return false;
    }
}
