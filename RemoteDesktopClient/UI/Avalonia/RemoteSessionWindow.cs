using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using RemoteDesktopClient.Core.Control;
using RemoteDesktopClient.Native.Windows;
using RemoteDesktopClient.UI.Avalonia.Components.Shared;
using SIPSorceryMedia.Abstractions;

namespace RemoteDesktopClient.UI.Avalonia;

internal sealed class RemoteSessionWindow : Window
{
    private readonly Image _image;
    private readonly TextBlock _statusText;
    private readonly TextBlock? _debugHeaderText;
    private WriteableBitmap? _bitmap;
    private PixelSize _bitmapSize;
    private bool _loggedUnsupportedFormat;

    public WriteableBitmap? CurrentFrame => _bitmap;

    public event Action? FrameRendered;

    public event Action<int, int>? LiveVideoSettingsRequested;

    public bool MouseControlEnabled { get; set; } = true;

    public bool KeyboardControlEnabled { get; set; } = true;

    public bool ClipboardSyncEnabled { get; set; } = true;

    public event Action<ControlMessage>? ControlMessageCaptured;

    private readonly IRemoteClipboard _clipboard = OperatingSystem.IsWindows() ? new Win32Clipboard() : new NullRemoteClipboard();
    private readonly IClipboardWatcher _clipboardWatcher = OperatingSystem.IsWindows() ? new Win32ClipboardWatcher() : new NullClipboardWatcher();

    private string? _lastSyncedClipboardText;
    private byte[]? _lastSyncedClipboardImage;
    private IReadOnlyList<string>? _lastSyncedClipboardFiles;

    public RemoteSessionWindow(string peerLabel, bool showDebugHeader = false)
    {
        Title = "Remote Session";
        Width = 1024;
        Height = 768;
        Background = Brushes.Black;

        _image = new Image
        {
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };

        _statusText = new TextBlock
        {
            Text = $"Waiting for video from {peerLabel}...",
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var layer = new Panel();
        layer.Children.Add(_image);
        layer.Children.Add(_statusText);

        if (showDebugHeader)
        {
            _debugHeaderText = new TextBlock
            {
                Text = "…",
                Foreground = Brushes.Lime,
                FontFamily = FontFamily.Parse("Consolas,monospace"),
                FontSize = 13,
            };

            var fpsInput = new TextBox { PlaceholderText = "fps", Width = 50, FontSize = 12 };
            var bitrateInput = new TextBox { PlaceholderText = "kbps", Width = 70, FontSize = 12 };
            var applyButton = new ModernButton { Text = "Apply", Variant = ButtonVariant.Subtle, Width = 64, Height = 26 };
            var applyStatusText = new TextBlock
            {
                Text = " ",
                Foreground = Brushes.Orange,
                FontFamily = FontFamily.Parse("Consolas,monospace"),
                FontSize = 11,
                Margin = new Thickness(0, 2, 0, 0),
            };
            applyButton.Click += (_, _) =>
            {
                if (int.TryParse(fpsInput.Text, out var fps) && fps > 0
                    && int.TryParse(bitrateInput.Text, out var bitrateKbps) && bitrateKbps > 0)
                {
                    LiveVideoSettingsRequested?.Invoke(fps, bitrateKbps);
                    applyStatusText.Text = $"Sent: {fps} fps, {bitrateKbps} kbps";
                }
                else
                {
                    applyStatusText.Text = "Enter a positive fps and kbps.";
                }
            };
            var inputRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Margin = new Thickness(0, 4, 0, 0) };
            inputRow.Children.Add(fpsInput);
            inputRow.Children.Add(bitrateInput);
            inputRow.Children.Add(applyButton);

            var debugHeaderStack = new StackPanel();
            debugHeaderStack.Children.Add(_debugHeaderText);
            debugHeaderStack.Children.Add(inputRow);
            debugHeaderStack.Children.Add(applyStatusText);

            var debugHeader = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 4),
                Margin = new Thickness(10),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Child = debugHeaderStack,
            };
            layer.Children.Add(debugHeader);
        }

        Content = layer;

        _image.PointerMoved += OnPointerMoved;
        _image.PointerPressed += OnPointerPressed;
        _image.PointerReleased += OnPointerReleased;
        _image.PointerWheelChanged += OnPointerWheelChanged;

        KeyDown += OnKeyDown;
        KeyUp += OnKeyUp;

        Opened += (_, _) =>
        {
            Focus();
            if (TryGetPlatformHandle() is { } platformHandle)
                _clipboardWatcher.Attach(platformHandle.Handle);
        };
        _clipboardWatcher.ClipboardChanged += OnLocalClipboardChanged;
        Closed += (_, _) => _clipboardWatcher.Dispose();
    }

    private void OnLocalClipboardChanged()
    {
        if (!ClipboardSyncEnabled)
            return;

        if (_clipboard.TryGetFilePaths(out var paths))
        {
            if (_lastSyncedClipboardFiles is { } lastFiles && lastFiles.SequenceEqual(paths, StringComparer.OrdinalIgnoreCase))
                return;
            _lastSyncedClipboardFiles = paths;
            ControlMessageCaptured?.Invoke(new ClipboardFilesMessage(paths));
            return;
        }
        _lastSyncedClipboardFiles = null;

        if (_clipboard.TryGetImagePng(out var png))
        {
            if (_lastSyncedClipboardImage is { } lastImage && lastImage.AsSpan().SequenceEqual(png))
                return;
            _lastSyncedClipboardImage = png;
            ControlMessageCaptured?.Invoke(new ClipboardImageMessage(png));
            return;
        }

        if (_clipboard.TryGetText(out var text))
        {
            if (text == _lastSyncedClipboardText)
                return;
            _lastSyncedClipboardText = text;
            ControlMessageCaptured?.Invoke(new ClipboardTextMessage(text));
        }
    }

    public void ApplyRemoteClipboard(ControlMessage message)
    {
        if (!ClipboardSyncEnabled)
            return;

        switch (message)
        {
            case ClipboardTextMessage text:
                _lastSyncedClipboardText = text.Text;
                _clipboard.SetText(text.Text);
                break;
            case ClipboardImageMessage image:
                _lastSyncedClipboardImage = image.PngBytes;
                _clipboard.SetImagePng(image.PngBytes);
                break;
            case ClipboardFilesMessage files:
                _lastSyncedClipboardFiles = files.Paths;
                _clipboard.SetFilePaths(files.Paths);
                break;
        }
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (MouseControlEnabled && TryGetNormalizedPosition(e, out var x, out var y))
            ControlMessageCaptured?.Invoke(new MouseMoveMessage(x, y));
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!MouseControlEnabled || !TryGetNormalizedPosition(e, out var x, out var y))
            return;
        if (TryGetButton(e.GetCurrentPoint(_image).Properties.PointerUpdateKind, out var button))
            ControlMessageCaptured?.Invoke(new MouseButtonMessage(button, MouseButtonAction.Down, x, y));
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!MouseControlEnabled || !TryGetNormalizedPosition(e, out var x, out var y))
            return;
        var button = e.InitialPressMouseButton switch
        {
            MouseButton.Left => MouseButtonKind.Left,
            MouseButton.Right => MouseButtonKind.Right,
            MouseButton.Middle => MouseButtonKind.Middle,
            _ => (MouseButtonKind?)null,
        };
        if (button is { } b)
            ControlMessageCaptured?.Invoke(new MouseButtonMessage(b, MouseButtonAction.Up, x, y));
    }

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (MouseControlEnabled && TryGetNormalizedPosition(e, out var x, out var y))
            ControlMessageCaptured?.Invoke(new MouseWheelMessage(x, y, e.Delta.Y));
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Source is TextBox)
            return;
        if (!KeyboardControlEnabled || !TryMapKey(e.Key, out var key))
            return;
        e.Handled = true;
        ControlMessageCaptured?.Invoke(new KeyDownMessage(key));
    }

    private void OnKeyUp(object? sender, KeyEventArgs e)
    {
        if (e.Source is TextBox)
            return;
        if (!KeyboardControlEnabled || !TryMapKey(e.Key, out var key))
            return;
        e.Handled = true;
        ControlMessageCaptured?.Invoke(new KeyUpMessage(key));
    }

    private static bool TryMapKey(Key avaloniaKey, out RemoteKey key)
    {
        if (avaloniaKey is >= Key.A and <= Key.Z)
        {
            key = RemoteKey.A + (avaloniaKey - Key.A);
            return true;
        }
        if (avaloniaKey is >= Key.D0 and <= Key.D9)
        {
            key = RemoteKey.D0 + (avaloniaKey - Key.D0);
            return true;
        }
        if (avaloniaKey is >= Key.F1 and <= Key.F12)
        {
            key = RemoteKey.F1 + (avaloniaKey - Key.F1);
            return true;
        }

        switch (avaloniaKey)
        {
            case Key.Enter: key = RemoteKey.Enter; return true;
            case Key.Escape: key = RemoteKey.Escape; return true;
            case Key.Back: key = RemoteKey.Backspace; return true;
            case Key.Tab: key = RemoteKey.Tab; return true;
            case Key.Space: key = RemoteKey.Space; return true;
            case Key.LeftShift: key = RemoteKey.ShiftLeft; return true;
            case Key.RightShift: key = RemoteKey.ShiftRight; return true;
            case Key.LeftCtrl: key = RemoteKey.ControlLeft; return true;
            case Key.RightCtrl: key = RemoteKey.ControlRight; return true;
            case Key.LeftAlt: key = RemoteKey.AltLeft; return true;
            case Key.RightAlt: key = RemoteKey.AltRight; return true;
            case Key.LWin: key = RemoteKey.WindowsLeft; return true;
            case Key.RWin: key = RemoteKey.WindowsRight; return true;
            case Key.Up: key = RemoteKey.ArrowUp; return true;
            case Key.Down: key = RemoteKey.ArrowDown; return true;
            case Key.Left: key = RemoteKey.ArrowLeft; return true;
            case Key.Right: key = RemoteKey.ArrowRight; return true;
            case Key.Home: key = RemoteKey.Home; return true;
            case Key.End: key = RemoteKey.End; return true;
            case Key.PageUp: key = RemoteKey.PageUp; return true;
            case Key.PageDown: key = RemoteKey.PageDown; return true;
            case Key.Insert: key = RemoteKey.Insert; return true;
            case Key.Delete: key = RemoteKey.Delete; return true;
            default: key = default; return false;
        }
    }

    private static bool TryGetButton(PointerUpdateKind updateKind, out MouseButtonKind button)
    {
        switch (updateKind)
        {
            case PointerUpdateKind.LeftButtonPressed:
                button = MouseButtonKind.Left;
                return true;
            case PointerUpdateKind.RightButtonPressed:
                button = MouseButtonKind.Right;
                return true;
            case PointerUpdateKind.MiddleButtonPressed:
                button = MouseButtonKind.Middle;
                return true;
            default:
                button = default;
                return false;
        }
    }

    private bool TryGetNormalizedPosition(PointerEventArgs e, out double normalizedX, out double normalizedY)
    {
        normalizedX = normalizedY = 0;
        if (_bitmap is null)
            return false;

        var controlWidth = _image.Bounds.Width;
        var controlHeight = _image.Bounds.Height;
        if (controlWidth <= 0 || controlHeight <= 0)
            return false;

        var imageAspect = (double)_bitmapSize.Width / _bitmapSize.Height;
        var controlAspect = controlWidth / controlHeight;

        double renderedWidth, renderedHeight, offsetX, offsetY;
        if (imageAspect > controlAspect)
        {
            renderedWidth = controlWidth;
            renderedHeight = controlWidth / imageAspect;
            offsetX = 0;
            offsetY = (controlHeight - renderedHeight) / 2;
        }
        else
        {
            renderedHeight = controlHeight;
            renderedWidth = controlHeight * imageAspect;
            offsetX = (controlWidth - renderedWidth) / 2;
            offsetY = 0;
        }

        var position = e.GetPosition(_image);
        var x = (position.X - offsetX) / renderedWidth;
        var y = (position.Y - offsetY) / renderedHeight;
        if (x < 0 || x > 1 || y < 0 || y > 1)
            return false;

        normalizedX = x;
        normalizedY = y;
        return true;
    }

    public void UpdateDebugStats(bool hasVideo, int bitrateKbps, double fps, bool isRelayed)
    {
        if (_debugHeaderText is null)
            return;

        _debugHeaderText.Text = hasVideo
            ? $"{bitrateKbps} kbps  |  {fps:0.0} fps  |  {(isRelayed ? "TURN" : "Direct")}"
            : "no video";
    }

    public void UpdateFrame(byte[] sample, uint width, uint height, int stride, VideoPixelFormatsEnum pixelFormat)
    {
        var avaloniaFormat = pixelFormat switch
        {
            VideoPixelFormatsEnum.Bgra => PixelFormats.Bgra8888,
            VideoPixelFormatsEnum.Rgba => PixelFormats.Rgba8888,
            VideoPixelFormatsEnum.Bgr => PixelFormats.Bgr24,
            VideoPixelFormatsEnum.Rgb => PixelFormats.Rgb24,
            _ => (PixelFormat?)null,
        };
        if (avaloniaFormat is null)
        {
            if (!_loggedUnsupportedFormat)
            {
                _loggedUnsupportedFormat = true;
                Trace.WriteLine($"RemoteSessionWindow: pixel format {pixelFormat} has no direct Avalonia mapping and is not yet converted - video frames in this format are skipped.");
                _statusText.Text = $"Received video in an unsupported format ({pixelFormat}).";
            }
            return;
        }

        var size = new PixelSize((int)width, (int)height);
        if (_bitmap is null || _bitmapSize != size)
        {
            _bitmap?.Dispose();
            _bitmap = new WriteableBitmap(size, new Vector(96, 96), avaloniaFormat, AlphaFormat.Opaque);
            _bitmapSize = size;
            _image.Source = _bitmap;
        }

        using (var frameBuffer = _bitmap.Lock())
        {
            var rowBytesToCopy = Math.Min(stride, frameBuffer.RowBytes);
            for (var row = 0; row < height; row++)
            {
                var destination = frameBuffer.Address + row * frameBuffer.RowBytes;
                Marshal.Copy(sample, row * stride, destination, rowBytesToCopy);
            }
        }

        if (_statusText.IsVisible)
            _statusText.IsVisible = false;
        _image.InvalidateVisual();
        FrameRendered?.Invoke();
    }
}
