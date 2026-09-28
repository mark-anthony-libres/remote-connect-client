using System.Runtime.InteropServices;
using RemoteDesktopClient.Core.Control;

namespace RemoteDesktopClient.Native.Windows;

public sealed class Win32InputInjector : IRemoteInputInjector
{
    private const uint InputMouse = 0;
    private const uint InputKeyboard = 1;

    private const uint MouseEventFMove = 0x0001;
    private const uint MouseEventFLeftDown = 0x0002;
    private const uint MouseEventFLeftUp = 0x0004;
    private const uint MouseEventFRightDown = 0x0008;
    private const uint MouseEventFRightUp = 0x0010;
    private const uint MouseEventFMiddleDown = 0x0020;
    private const uint MouseEventFMiddleUp = 0x0040;
    private const uint MouseEventFWheel = 0x0800;
    private const uint MouseEventFAbsolute = 0x8000;

    private const uint KeyEventFKeyUp = 0x0002;

    private const int WheelDelta = 120;
    private const int AbsoluteCoordinateMax = 65535;

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputUnion Union;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MouseInputData Mouse;
        [FieldOffset(0)] public KeyboardInputData Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInputData
    {
        public int Dx;
        public int Dy;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInputData
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint numInputs, Input[] inputs, int inputSize);

    public void MoveMouse(double normalizedX, double normalizedY)
    {
        var (x, y) = ToAbsolute(normalizedX, normalizedY);
        Send(MouseInput(x, y, 0, MouseEventFMove | MouseEventFAbsolute));
    }

    public void MouseButton(MouseButtonKind button, MouseButtonAction action, double normalizedX, double normalizedY)
    {
        var flag = (button, action) switch
        {
            (MouseButtonKind.Left, MouseButtonAction.Down) => MouseEventFLeftDown,
            (MouseButtonKind.Left, MouseButtonAction.Up) => MouseEventFLeftUp,
            (MouseButtonKind.Right, MouseButtonAction.Down) => MouseEventFRightDown,
            (MouseButtonKind.Right, MouseButtonAction.Up) => MouseEventFRightUp,
            (MouseButtonKind.Middle, MouseButtonAction.Down) => MouseEventFMiddleDown,
            (MouseButtonKind.Middle, MouseButtonAction.Up) => MouseEventFMiddleUp,
            _ => throw new ArgumentOutOfRangeException(nameof(button)),
        };

        var (x, y) = ToAbsolute(normalizedX, normalizedY);
        Send(
            MouseInput(x, y, 0, MouseEventFMove | MouseEventFAbsolute),
            MouseInput(0, 0, 0, flag));
    }

    public void MouseWheel(double normalizedX, double normalizedY, double deltaY)
    {
        var (x, y) = ToAbsolute(normalizedX, normalizedY);
        var wheelData = unchecked((uint)(int)Math.Round(deltaY * WheelDelta));
        Send(
            MouseInput(x, y, 0, MouseEventFMove | MouseEventFAbsolute),
            MouseInput(0, 0, wheelData, MouseEventFWheel));
    }

    public void KeyDown(RemoteKey key) => Send(KeyboardInput(ToVirtualKeyCode(key), 0));

    public void KeyUp(RemoteKey key) => Send(KeyboardInput(ToVirtualKeyCode(key), KeyEventFKeyUp));

    private static ushort ToVirtualKeyCode(RemoteKey key)
    {
        if (key is >= RemoteKey.A and <= RemoteKey.Z)
            return (ushort)('A' + (key - RemoteKey.A));
        if (key is >= RemoteKey.D0 and <= RemoteKey.D9)
            return (ushort)('0' + (key - RemoteKey.D0));
        if (key is >= RemoteKey.F1 and <= RemoteKey.F12)
            return (ushort)(0x70 + (key - RemoteKey.F1));

        return key switch
        {
            RemoteKey.Enter => 0x0D,
            RemoteKey.Escape => 0x1B,
            RemoteKey.Backspace => 0x08,
            RemoteKey.Tab => 0x09,
            RemoteKey.Space => 0x20,
            RemoteKey.ShiftLeft => 0xA0,
            RemoteKey.ShiftRight => 0xA1,
            RemoteKey.ControlLeft => 0xA2,
            RemoteKey.ControlRight => 0xA3,
            RemoteKey.AltLeft => 0xA4,
            RemoteKey.AltRight => 0xA5,
            RemoteKey.WindowsLeft => 0x5B,
            RemoteKey.WindowsRight => 0x5C,
            RemoteKey.ArrowLeft => 0x25,
            RemoteKey.ArrowUp => 0x26,
            RemoteKey.ArrowRight => 0x27,
            RemoteKey.ArrowDown => 0x28,
            RemoteKey.Home => 0x24,
            RemoteKey.End => 0x23,
            RemoteKey.PageUp => 0x21,
            RemoteKey.PageDown => 0x22,
            RemoteKey.Insert => 0x2D,
            RemoteKey.Delete => 0x2E,
            _ => throw new ArgumentOutOfRangeException(nameof(key), key, "Unmapped RemoteKey."),
        };
    }

    private static Input KeyboardInput(ushort virtualKey, uint flags) => new()
    {
        Type = InputKeyboard,
        Union = new InputUnion
        {
            Keyboard = new KeyboardInputData { VirtualKey = virtualKey, Flags = flags },
        },
    };

    private static (int X, int Y) ToAbsolute(double normalizedX, double normalizedY) => (
        (int)Math.Round(Math.Clamp(normalizedX, 0.0, 1.0) * AbsoluteCoordinateMax),
        (int)Math.Round(Math.Clamp(normalizedY, 0.0, 1.0) * AbsoluteCoordinateMax));

    private static Input MouseInput(int dx, int dy, uint mouseData, uint flags) => new()
    {
        Type = InputMouse,
        Union = new InputUnion
        {
            Mouse = new MouseInputData { Dx = dx, Dy = dy, MouseData = mouseData, Flags = flags },
        },
    };

    private static void Send(params Input[] inputs) => SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
}
