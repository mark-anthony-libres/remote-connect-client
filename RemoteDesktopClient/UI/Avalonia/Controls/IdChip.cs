using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media;

namespace RemoteDesktopClient.UI.Avalonia.Controls;

/// <summary>A white, bordered pill showing a device ID with a clipboard icon
/// on the left and a copy icon on the right. Clicking anywhere on it copies
/// the ID to the clipboard.</summary>
internal sealed class IdChip : Control
{
    private string _deviceId = string.Empty;
    private bool _isHovered;

    public string DeviceId
    {
        get => _deviceId;
        set
        {
            _deviceId = value;
            AutoSizeToContent();
            InvalidateVisual();
        }
    }

    public event EventHandler? CopyRequested;

    public IdChip()
    {
        Height = 44;
        Cursor = new Cursor(StandardCursorType.Hand);

        PointerEntered += (_, _) => { _isHovered = true; InvalidateVisual(); };
        PointerExited += (_, _) => { _isHovered = false; InvalidateVisual(); };
        PointerPressed += async (_, _) =>
        {
            if (!string.IsNullOrEmpty(_deviceId))
            {
                var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
                if (clipboard is not null)
                    await clipboard.SetTextAsync(_deviceId);
            }
            CopyRequested?.Invoke(this, EventArgs.Empty);
        };
    }

    private void AutoSizeToContent()
    {
        var measured = TextRendering.Format(_deviceId, AppTheme.FontMonoId, AppTheme.Accent);
        Width = measured.WidthIncludingTrailingWhitespace + 96; // room for the clipboard icon, padding and copy icon
    }

    public override void Render(DrawingContext context)
    {
        var rect = new Rect(0, 0, Bounds.Width, Bounds.Height);
        var fill = _isHovered ? AppTheme.SubtleButtonBackground : AppTheme.CardBackground;
        context.DrawRectangle(new SolidColorBrush(fill), new Pen(new SolidColorBrush(AppTheme.CardBorder), 1), new RoundedRect(rect, 10));

        var clipboardBounds = new Rect(14, Bounds.Height / 2 - 9, 18, 18);
        Icons.Draw(context, IconKind.Clipboard, clipboardBounds, AppTheme.TextSecondary, 1.4);

        var textRect = new Rect(40, 0, Math.Max(0, Bounds.Width - 40 - 36), Bounds.Height);
        TextRendering.DrawVerticalCenter(context, _deviceId, AppTheme.FontMonoId, AppTheme.Accent, textRect);

        var copyBounds = new Rect(Bounds.Width - 30, Bounds.Height / 2 - 8, 16, 16);
        Icons.Draw(context, IconKind.Copy, copyBounds, AppTheme.TextSecondary, 1.4);
    }
}
