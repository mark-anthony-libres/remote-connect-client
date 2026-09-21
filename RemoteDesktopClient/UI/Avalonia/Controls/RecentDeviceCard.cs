using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using RemoteDesktopClient.Core.Device;

namespace RemoteDesktopClient.UI.Avalonia.Controls;

/// <summary>One card in the Recent Connections grid: a photo banner for the
/// device (image + status, with a small device-type badge), then name, ID,
/// last-connected time, a Connect button and a small overflow menu button.
/// Uses Canvas + absolute positions to mirror the original pixel layout exactly.</summary>
internal sealed class RecentDeviceCard : Canvas
{
    // Loaded once and reused across every card instance rather than
    // re-decoding the same file per card.
    private static readonly Bitmap BannerImage = new(
        AssetLoader.Open(new Uri("avares://RemoteDesktopClient/Assets/Images/device-banner.png")));

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

        // Photo banner for the device — rounded only on top so it sits flush
        // with the card underneath it. Border (not RoundedBackground) here
        // specifically because it supports an ImageBrush background with
        // independent corner radii natively, cropped/filled via Stretch.
        var banner = new Border
        {
            Width = width,
            Height = bannerHeight,
            CornerRadius = new CornerRadius(12, 12, 0, 0),
            ClipToBounds = true,
            Background = new ImageBrush(BannerImage)
            {
                Stretch = Stretch.UniformToFill,
                AlignmentY = AlignmentY.Top,
            },
        };
        Place(banner, 0, 0);
        Children.Add(banner);

        // Small device-type badge, overlaid bottom-left on the photo — keeps
        // the at-a-glance device type the old full-size icon badge gave,
        // without competing with the photo for visual weight.
        const int smallIconSize = 32;
        var iconBadge = new IconBadge
        {
            Icon = icon,
            TintBackground = iconBackground,
            TintForeground = iconForeground,
            Width = smallIconSize,
            Height = smallIconSize,
            CornerRadius = 10,
        };
        Place(iconBadge, 12, bannerHeight - smallIconSize - 12);

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
