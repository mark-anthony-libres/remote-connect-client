using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace RemoteDesktopClient.UI.Avalonia.Controls;

/// <summary>A window caption button (minimize/maximize/restore/close) — square
/// hover region and hairline glyph, matching Windows' own title bar buttons,
/// not the rounded-pill style ModernButton uses elsewhere in this app.</summary>
internal sealed class CaptionButton : Control
{
    private bool _isHovered;
    private bool _isPressed;

    public IconKind Icon { get; set; } = IconKind.WindowMinimize;

    /// <summary>True for the Close button — it turns red on hover, the other
    /// three turn a neutral gray.</summary>
    public bool IsCloseButton { get; set; }

    public event EventHandler? Click;

    public CaptionButton()
    {
        Width = 46;
        Height = 32;
        Cursor = new Cursor(StandardCursorType.Arrow);

        PointerEntered += (_, _) => { _isHovered = true; InvalidateVisual(); };
        PointerExited += (_, _) => { _isHovered = false; _isPressed = false; InvalidateVisual(); };
        // e.Handled = true on both: without it, the press/release bubbles up
        // to the title bar's own PointerPressed handler, which calls
        // BeginMoveDrag() for any click landing anywhere in the title bar —
        // starting a native window-drag on top of the button press and
        // swallowing the click before it ever reaches us.
        PointerPressed += (_, e) => { _isPressed = true; InvalidateVisual(); e.Handled = true; };
        PointerReleased += (_, e) =>
        {
            bool wasPressed = _isPressed;
            _isPressed = false;
            InvalidateVisual();
            if (wasPressed && new Rect(Bounds.Size).Contains(e.GetPosition(this)))
                Click?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        };
    }

    public override void Render(DrawingContext context)
    {
        var rect = new Rect(0, 0, Bounds.Width, Bounds.Height);
        Color? background = (_isHovered, IsCloseButton) switch
        {
            (true, true) => Color.FromRgb(0xE8, 0x11, 0x23),
            (true, false) => AppTheme.SubtleButtonBackground,
            _ => null,
        };
        if (background is { } bg)
            context.DrawRectangle(new SolidColorBrush(bg), null, rect);

        var glyphColor = _isHovered && IsCloseButton ? Colors.White : AppTheme.TextSecondary;
        const double glyphSize = 10;
        var glyphBounds = new Rect(
            (Bounds.Width - glyphSize) / 2, (Bounds.Height - glyphSize) / 2, glyphSize, glyphSize);
        Icons.Draw(context, Icon, glyphBounds, glyphColor, 1.2);
    }
}
