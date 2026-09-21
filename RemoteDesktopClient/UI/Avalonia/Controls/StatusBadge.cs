using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace RemoteDesktopClient.UI.Avalonia.Controls;

/// <summary>A colored pill showing whether a device is online or offline.</summary>
internal sealed class StatusBadge : Control
{
    public bool IsOnline { get; set; }

    public StatusBadge()
    {
        Width = 76;
        Height = 22;
    }

    public override void Render(DrawingContext context)
    {
        var pillColor = IsOnline ? AppTheme.OnlineSoft : AppTheme.OfflineSoft;
        var dotColor = IsOnline ? AppTheme.Online : AppTheme.Offline;
        string text = IsOnline ? "Online" : "Offline";

        var rect = new Rect(0, 0, Bounds.Width, Bounds.Height);
        context.DrawRectangle(new SolidColorBrush(pillColor), null, new RoundedRect(rect, Bounds.Height / 2));

        const int dotSize = 7;
        double dotX = 10;
        double dotY = (Bounds.Height - dotSize) / 2;
        context.DrawEllipse(new SolidColorBrush(dotColor), null, new Point(dotX + dotSize / 2.0, dotY + dotSize / 2.0), dotSize / 2.0, dotSize / 2.0);

        var textRect = new Rect(dotX + dotSize + 6, 0, Bounds.Width - dotX - dotSize - 12, Bounds.Height);
        TextRendering.DrawVerticalCenter(context, text, AppTheme.FontSmall, dotColor, textRect);
    }
}
