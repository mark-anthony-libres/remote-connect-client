using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace RemoteDesktopClient.UI.Avalonia.Controls;

/// <summary>A small icon followed by a line of text — e.g. a clock icon next to
/// "Last connected: 2 hours ago".</summary>
internal sealed class IconLabel : Control
{
    public IconKind Icon { get; set; } = IconKind.Clock;
    public Color IconColor { get; set; } = AppTheme.TextSecondary;
    public string Caption { get; set; } = string.Empty;

    public IconLabel()
    {
        Height = 16;
    }

    public override void Render(DrawingContext context)
    {
        var iconBounds = new Rect(0, (Bounds.Height - 12) / 2, 12, 12);
        Icons.Draw(context, Icon, iconBounds, IconColor, 1.3);

        var textRect = new Rect(18, 0, Math.Max(0, Bounds.Width - 18), Bounds.Height);
        TextRendering.DrawVerticalCenter(context, Caption, AppTheme.FontSmall, AppTheme.TextSecondary, textRect);
    }
}
