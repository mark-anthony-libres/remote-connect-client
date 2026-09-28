using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using RemoteDesktopClient.UI.Avalonia;
using RemoteDesktopClient.UI.Avalonia.Components.Shared;

namespace RemoteDesktopClient.UI.Avalonia.Components.HeaderBar;

internal sealed class AvatarBadge : Control
{
    public override void Render(DrawingContext context)
    {
        var circleRect = new Rect(0, 2, 36, 36);
        context.DrawEllipse(new SolidColorBrush(AppTheme.AccentSoft), null, circleRect.Center, 18, 18);
        TextRendering.DrawVerticalCenter(context, "M", AppTheme.FontBodyBold, AppTheme.AccentSoftText, circleRect, TextAlign.Center);

        var chevronBounds = new Rect(42, 15, 12, 10);
        Icons.Draw(context, IconKind.Chevron, chevronBounds, AppTheme.TextSecondary, 1.5);
    }
}
