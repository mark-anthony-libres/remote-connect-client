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

    public override void Render(DrawingContext context)
    {
        var rect = new Rect(0, 0, Bounds.Width, Bounds.Height);
        var pen = Stroke is { } stroke ? new Pen(new SolidColorBrush(stroke), StrokeThickness) : null;
        context.DrawRectangle(new SolidColorBrush(Fill), pen, new RoundedRect(rect, CornerRadius));
    }
}
