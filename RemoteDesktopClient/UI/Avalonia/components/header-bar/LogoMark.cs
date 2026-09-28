using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using RemoteDesktopClient.UI.Avalonia;

namespace RemoteDesktopClient.UI.Avalonia.Components.HeaderBar;

internal sealed class LogoMark : Control
{
    public override void Render(DrawingContext context)
    {
        var rect = new Rect(0, 0, Bounds.Width, Bounds.Height);
        context.DrawRectangle(new SolidColorBrush(AppTheme.Accent), null, new RoundedRect(rect, 11));
        context.DrawEllipse(Brushes.White, null, rect.Center, 5, 5);
    }
}
