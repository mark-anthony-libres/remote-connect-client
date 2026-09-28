using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using RemoteDesktopClient.UI.Avalonia;

namespace RemoteDesktopClient.UI.Avalonia.Components.Shared;

internal static class SharedBuilders
{
    public static TextBlock TextBlockOf(string text, AppFont font, Color color, Thickness? margin = null) => new()
    {
        Text = text,
        FontFamily = font.Family,
        FontSize = font.Size,
        FontWeight = font.Weight,
        FontStyle = font.Style,
        Foreground = new SolidColorBrush(color),
        Margin = margin ?? default,
    };
}
