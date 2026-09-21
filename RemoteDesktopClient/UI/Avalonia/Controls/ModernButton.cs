using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace RemoteDesktopClient.UI.Avalonia.Controls;

internal enum ButtonVariant
{
    /// <summary>Filled, accent-colored — for the main action in a section.</summary>
    Primary,

    /// <summary>Light neutral background — for quiet actions like "Settings" or "Copy ID".</summary>
    Subtle,

    /// <summary>Light accent-tinted background — for repeated row actions like "Connect" in a list.</summary>
    SoftAccent
}

/// <summary>A flat, rounded button with hover/press feedback, styled per <see cref="ButtonVariant"/>.
/// Deliberately a plain Control (not Avalonia's templated Button) — fully custom-rendered, with no
/// default theme chrome to override or fight.</summary>
internal sealed class ModernButton : Control
{
    private bool _isHovered;
    private bool _isPressed;
    private bool _isEnabled = true;

    public ButtonVariant Variant { get; set; } = ButtonVariant.Primary;
    public double CornerRadius { get; set; } = 8;
    public string Text { get; set; } = string.Empty;

    /// <summary>Optional icon drawn to the left of the text. Null draws no icon.</summary>
    public IconKind? Icon { get; set; }

    public new bool IsEnabled
    {
        get => _isEnabled;
        set { _isEnabled = value; InvalidateVisual(); }
    }

    public event EventHandler? Click;

    public ModernButton()
    {
        Height = 36;
        Focusable = true;
        Cursor = new Cursor(StandardCursorType.Hand);

        PointerEntered += (_, _) => { _isHovered = true; InvalidateVisual(); };
        PointerExited += (_, _) => { _isHovered = false; _isPressed = false; InvalidateVisual(); };
        PointerPressed += (_, e) =>
        {
            if (!_isEnabled) return;
            _isPressed = true;
            InvalidateVisual();
        };
        PointerReleased += (_, e) =>
        {
            if (!_isEnabled) { _isPressed = false; return; }
            bool wasPressed = _isPressed;
            _isPressed = false;
            InvalidateVisual();
            if (wasPressed && new Rect(Bounds.Size).Contains(e.GetPosition(this)))
                PerformClick();
        };
        KeyDown += (_, e) =>
        {
            if (_isEnabled && (e.Key == Key.Enter || e.Key == Key.Space))
                PerformClick();
        };
    }

    public void PerformClick()
    {
        if (_isEnabled) Click?.Invoke(this, EventArgs.Empty);
    }

    public override void Render(DrawingContext context)
    {
        var (background, textColor) = GetColors();

        var rect = new Rect(0, 0, Bounds.Width, Bounds.Height);
        context.DrawRectangle(new SolidColorBrush(background), null, new RoundedRect(rect, CornerRadius));

        if (Icon is { } icon)
        {
            var measured = TextRendering.Format(Text, AppTheme.FontBodyBold, textColor);
            const int iconSize = 15;
            const int gap = 8;
            double groupWidth = iconSize + gap + measured.WidthIncludingTrailingWhitespace;
            double startX = rect.Left + (rect.Width - groupWidth) / 2;

            var iconBounds = new Rect(startX, (Bounds.Height - iconSize) / 2, iconSize, iconSize);
            Icons.Draw(context, icon, iconBounds, textColor, 1.6);

            var textRect = new Rect(startX + iconSize + gap, 0, measured.WidthIncludingTrailingWhitespace + 4, Bounds.Height);
            TextRendering.DrawVerticalCenter(context, Text, AppTheme.FontBodyBold, textColor, textRect);
        }
        else
        {
            TextRendering.DrawVerticalCenter(context, Text, AppTheme.FontBodyBold, textColor, rect, TextAlign.Center);
        }
    }

    private (Color Background, Color Text) GetColors()
    {
        if (!_isEnabled)
            return (AppTheme.SubtleButtonBackground, AppTheme.TextSecondary);

        return Variant switch
        {
            ButtonVariant.Primary => _isPressed
                ? (AppTheme.AccentPressed, Colors.White)
                : _isHovered
                    ? (AppTheme.AccentHover, Colors.White)
                    : (AppTheme.Accent, Colors.White),
            ButtonVariant.Subtle => _isPressed || _isHovered
                ? (AppTheme.SubtleButtonHover, AppTheme.TextPrimary)
                : (AppTheme.SubtleButtonBackground, AppTheme.TextPrimary),
            ButtonVariant.SoftAccent => _isPressed || _isHovered
                ? (AppTheme.AccentSoftHover, AppTheme.AccentSoftText)
                : (AppTheme.AccentSoft, AppTheme.AccentSoftText),
            _ => (AppTheme.Accent, Colors.White),
        };
    }
}
