using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace RemoteDesktopClient.UI.Avalonia.Controls;

/// <summary>A rounded-rect fill + optional border stroke, sized to match its parent's
/// bounds exactly. Used as the first child of Panel/Canvas-derived composite controls
/// (IconTextBox, RecentDeviceCard) that need custom background painting: Avalonia's
/// Panel.Render is sealed, so those classes can't override Render() themselves.</summary>
internal sealed class RoundedBackground : Control
{
    public Color Fill { get; set; } = Colors.Transparent;
    public Color? Stroke { get; set; }
    public double StrokeThickness { get; set; } = 1;
    public double CornerRadius { get; set; }

    // Set any of these to override CornerRadius for just that corner — e.g. a
    // banner rounded only on top, flush-square on the bottom where it meets
    // the rest of the card underneath it.
    public double? TopLeftRadius { get; set; }
    public double? TopRightRadius { get; set; }
    public double? BottomRightRadius { get; set; }
    public double? BottomLeftRadius { get; set; }

    public override void Render(DrawingContext context)
    {
        var rect = new Rect(0, 0, Bounds.Width, Bounds.Height);
        var pen = Stroke is { } stroke ? new Pen(new SolidColorBrush(stroke), StrokeThickness) : null;
        var roundedRect = new RoundedRect(
            rect,
            TopLeftRadius ?? CornerRadius,
            TopRightRadius ?? CornerRadius,
            BottomRightRadius ?? CornerRadius,
            BottomLeftRadius ?? CornerRadius);
        context.DrawRectangle(new SolidColorBrush(Fill), pen, roundedRect);
    }
}
