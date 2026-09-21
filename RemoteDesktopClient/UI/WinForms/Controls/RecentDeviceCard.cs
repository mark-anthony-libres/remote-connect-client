using System.Drawing.Drawing2D;
using RemoteDesktopClient.Core.Device;

namespace RemoteDesktopClient.UI.WinForms.Controls;

/// <summary>One card in the Recent Connections grid: icon, name, status, ID,
/// last-connected time, a Connect button and a small overflow menu button.</summary>
internal sealed class RecentDeviceCard : Panel
{
    public event EventHandler? ConnectClicked;
    public event EventHandler? MenuClicked;

    public RecentDeviceCard(DeviceConnection device, IconKind icon, Color iconBackground, Color iconForeground)
    {
        const int padding = 18;
        const int menuButtonWidth = 40;
        const int buttonGap = 12;

        Size = new Size(252, 224);
        Margin = new Padding(0, 0, 20, 20);
        BackColor = Theme.CardBackground;

        var iconBadge = new IconBadge
        {
            Icon = icon,
            TintBackground = iconBackground,
            TintForeground = iconForeground,
            Size = new Size(40, 40),
            Location = new Point(padding, padding),
        };

        var statusBadge = new StatusBadge
        {
            IsOnline = device.Status == ConnectionStatus.Online,
            Size = new Size(76, 22),
        };
        statusBadge.Location = new Point(Width - padding - statusBadge.Width, padding + 4);

        var nameLabel = new Label
        {
            Text = string.IsNullOrWhiteSpace(device.DeviceName) ? "Unnamed device" : device.DeviceName,
            Font = Theme.FontBodyBold,
            ForeColor = Theme.TextPrimary,
            Location = new Point(padding, 70),
            Size = new Size(Width - padding * 2, 42),
            // Wraps to two lines (matching the reference) instead of
            // truncating with an ellipsis.
            AutoEllipsis = false,
        };

        var idLabel = new Label
        {
            Text = $"ID: {device.DeviceId}",
            Font = Theme.FontSmall,
            ForeColor = Theme.TextSecondary,
            Location = new Point(padding, 118),
            Size = new Size(Width - padding * 2, 18),
            AutoEllipsis = true,
        };

        var lastConnectedLabel = new IconLabel
        {
            Icon = IconKind.Clock,
            Caption = device.LastConnectedText,
            Location = new Point(padding, 142),
            Size = new Size(Width - padding * 2, 18),
        };

        int buttonRowY = 178;
        int buttonHeight = 34;
        int connectButtonWidth = Width - padding * 2 - menuButtonWidth - buttonGap;

        var connectButton = new ModernButton
        {
            Text = "Connect",
            Icon = IconKind.Play,
            Variant = ButtonVariant.SoftAccent,
            Location = new Point(padding, buttonRowY),
            Size = new Size(connectButtonWidth, buttonHeight),
        };
        connectButton.Click += (_, e) => ConnectClicked?.Invoke(this, e);

        var menuButton = new ModernButton
        {
            Text = string.Empty,
            Icon = IconKind.Dots,
            Variant = ButtonVariant.Subtle,
            Location = new Point(padding + connectButtonWidth + buttonGap, buttonRowY),
            Size = new Size(menuButtonWidth, buttonHeight),
        };
        menuButton.Click += (_, e) => MenuClicked?.Invoke(this, e);

        Controls.Add(iconBadge);
        Controls.Add(statusBadge);
        Controls.Add(nameLabel);
        Controls.Add(idLabel);
        Controls.Add(lastConnectedLabel);
        Controls.Add(connectButton);
        Controls.Add(menuButton);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path = RoundedRect.CreatePath(rect, 12);
        using var pen = new Pen(Theme.CardBorder);
        e.Graphics.DrawPath(pen, path);
    }
}
