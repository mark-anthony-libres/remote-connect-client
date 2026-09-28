using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using RemoteDesktopClient.UI.Avalonia.Components.Shared;

namespace RemoteDesktopClient.UI.Avalonia;

internal sealed class WebRtcTestMessageAlert : Window
{
    public WebRtcTestMessageAlert(string messageText)
    {
        Title = "WebRTC Test";
        WindowDecorations = WindowDecorations.None;
        CanResize = false;
        ShowInTaskbar = false;
        SizeToContent = SizeToContent.WidthAndHeight;
        Topmost = true;
        Background = new SolidColorBrush(AppTheme.CardBackground);

        var card = new Border
        {
            Background = new SolidColorBrush(AppTheme.CardBackground),
            BorderBrush = new SolidColorBrush(AppTheme.CardBorder),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(24),
            Width = 360,
        };

        var stack = new StackPanel { Spacing = 12 };
        stack.Children.Add(TextBlockOf("WebRTC Test", AppTheme.FontSectionHeader, AppTheme.TextPrimary));
        stack.Children.Add(TextBlockOf("Received:", AppTheme.FontSmall, AppTheme.TextSecondary));

        var messageBlock = TextBlockOf(messageText, AppTheme.FontBodyBold, AppTheme.Accent);
        messageBlock.TextWrapping = TextWrapping.Wrap;
        stack.Children.Add(messageBlock);

        var okButton = new ModernButton
        {
            Text = "OK",
            Variant = ButtonVariant.Primary,
            Width = 100,
            Height = 40,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 8, 0, 0),
        };
        okButton.Click += (_, _) => Close();
        stack.Children.Add(okButton);

        card.Child = stack;
        Content = card;

        Opened += (_, _) => CenterOnPrimaryScreen();
    }

    private void CenterOnPrimaryScreen()
    {
        if (Screens.Primary?.WorkingArea is not { } area)
            return;

        var width = (int)Math.Ceiling(Bounds.Width * RenderScaling);
        var height = (int)Math.Ceiling(Bounds.Height * RenderScaling);
        Position = new PixelPoint(area.X + (area.Width - width) / 2, area.Y + (area.Height - height) / 2);
    }

    private static TextBlock TextBlockOf(string text, AppFont font, Color color) => new()
    {
        Text = text,
        FontFamily = font.Family,
        FontSize = font.Size,
        FontWeight = font.Weight,
        FontStyle = font.Style,
        Foreground = new SolidColorBrush(color),
    };
}
