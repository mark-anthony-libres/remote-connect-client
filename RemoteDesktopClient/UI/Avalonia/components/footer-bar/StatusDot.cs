using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using RemoteDesktopClient.UI.Avalonia;

namespace RemoteDesktopClient.UI.Avalonia.Components.FooterBar;

internal sealed class StatusDot : Control
{
    public override void Render(DrawingContext context) =>
        context.DrawEllipse(new SolidColorBrush(AppTheme.Online), null, new Rect(0, 0, Bounds.Width, Bounds.Height).Center, Bounds.Width / 2, Bounds.Height / 2);
}
