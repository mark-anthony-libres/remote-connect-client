using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using RemoteDesktopClient.Core.Device;

namespace RemoteDesktopClient.UI.Avalonia.Controls;

/// <summary>One card in the Recent Connections grid: a tinted illustration
/// banner for the device (icon + status), then name, ID, last-connected
/// time, a Connect button and a small overflow menu button.
/// Uses Canvas + absolute positions to mirror the original pixel layout exactly.</summary>
internal sealed class RecentDeviceCard : Canvas
{
    public event EventHandler? ConnectClicked;
    public event EventHandler? MenuClicked;

    public RecentDeviceCard(DeviceConnection device, IconKind icon, Color iconBackground, Color iconForeground)
    {
        const int padding = 18;
        const int menuButtonWidth = 40;
        const int buttonGap = 12;
        const int width = 252;
        const int bannerHeight = 92;
        const int height = 260;

        Width = width;
        Height = height;

        // Panel.Render is sealed in Avalonia, so the border is painted by this
        // dedicated background child instead of an override (see RoundedBackground.cs).
        var background = new RoundedBackground { Width = width, Height = height, Stroke = AppTheme.CardBorder, CornerRadius = 12 };
        Place(background, 0, 0);
        Children.Add(background);

        // A larger, tinted illustration banner for the device — rounded only
        // on top so it sits flush with the card underneath it — replacing
        // the small corner icon with something closer to a device "photo".
        var banner = new RoundedBackground
        {
            Width = width,
            Height = bannerHeight,
            Fill = iconBackground,
            TopLeftRadius = 12,
            TopRightRadius = 12,
            BottomLeftRadius = 0,
            BottomRightRadius = 0,
        };
        Place(banner, 0, 0);
        Children.Add(banner);

        const int bigIconSize = 64;
        var iconBadge = new IconBadge
        {
            Icon = icon,
            TintBackground = AppTheme.CardBackground,
            TintForeground = iconForeground,
            Width = bigIconSize,
            Height = bigIconSize,
            CornerRadius = 16,
        };
        Place(iconBadge, (width - bigIconSize) / 2.0, (bannerHeight - bigIconSize) / 2.0);

        var statusBadge = new StatusBadge
        {
            IsOnline = device.Status == ConnectionStatus.Online,
            Width = 76,
            Height = 22,
        };
        Place(statusBadge, width - 14 - statusBadge.Width, 14);

        var nameLabel = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(device.DeviceName) ? "Unnamed device" : device.DeviceName,
            FontFamily = AppTheme.FontBodyBold.Family,
            FontWeight = AppTheme.FontBodyBold.Weight,
            FontSize = AppTheme.FontBodyBold.Size,
            Foreground = new SolidColorBrush(AppTheme.TextPrimary),
            Width = width - padding * 2,
            Height = 42,
            TextWrapping = TextWrapping.Wrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        Place(nameLabel, padding, bannerHeight + 14);

        var idLabel = new TextBlock
        {
            Text = $"ID: {device.DeviceId}",
            FontFamily = AppTheme.FontSmall.Family,
            FontSize = AppTheme.FontSmall.Size,
            Foreground = new SolidColorBrush(AppTheme.TextSecondary),
            Width = width - padding * 2,
            Height = 18,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        Place(idLabel, padding, bannerHeight + 62);

        var lastConnectedLabel = new IconLabel
        {
            Icon = IconKind.Clock,
            Caption = device.LastConnectedText,
            Width = width - padding * 2,
            Height = 18,
        };
        Place(lastConnectedLabel, padding, bannerHeight + 86);

        int buttonRowY = bannerHeight + 116;
        int buttonHeight = 34;
        int connectButtonWidth = width - padding * 2 - menuButtonWidth - buttonGap;

        var connectButton = new ModernButton
        {
            Text = "Connect",
            Icon = IconKind.Play,
            Variant = ButtonVariant.SoftAccent,
            Width = connectButtonWidth,
            Height = buttonHeight,
        };
        connectButton.Click += (_, e) => ConnectClicked?.Invoke(this, e);
        Place(connectButton, padding, buttonRowY);

        var menuButton = new ModernButton
        {
            Text = string.Empty,
            Icon = IconKind.Dots,
            Variant = ButtonVariant.Subtle,
            Width = menuButtonWidth,
            Height = buttonHeight,
        };
        menuButton.Click += (_, e) => MenuClicked?.Invoke(this, e);
        Place(menuButton, padding + connectButtonWidth + buttonGap, buttonRowY);

        Children.Add(iconBadge);
        Children.Add(statusBadge);
        Children.Add(nameLabel);
        Children.Add(idLabel);
        Children.Add(lastConnectedLabel);
        Children.Add(connectButton);
        Children.Add(menuButton);
    }

    private static void Place(Control control, double x, double y)
    {
        SetLeft(control, x);
        SetTop(control, y);
    }
}
