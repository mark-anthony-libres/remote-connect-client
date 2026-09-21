using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace RemoteDesktopClient.UI.Avalonia.Controls;

/// <summary>A rounded, tint-colored square with a centered icon — used throughout for
/// section markers and per-device icons.</summary>
internal sealed class IconBadge : Control
{
    public IconKind Icon { get; set; } = IconKind.Monitor;
    public Color TintBackground { get; set; } = AppTheme.IconBlueBg;
    public Color TintForeground { get; set; } = AppTheme.IconBlueFg;
    public double CornerRadius { get; set; } = 12;

    public IconBadge()
    {
        Width = 40;
        Height = 40;
    }

    public override void Render(DrawingContext context)
    {
        var rect = new Rect(0, 0, Bounds.Width, Bounds.Height);
        context.DrawRectangle(new SolidColorBrush(TintBackground), null, new RoundedRect(rect, CornerRadius));

        var iconBounds = new Rect(Bounds.Width * 0.26, Bounds.Height * 0.26, Bounds.Width * 0.48, Bounds.Height * 0.48);
        Icons.Draw(context, Icon, iconBounds, TintForeground, Math.Max(1.4, Bounds.Width * 0.06));
    }
}
