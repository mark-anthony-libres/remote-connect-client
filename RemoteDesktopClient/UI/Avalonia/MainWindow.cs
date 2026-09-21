using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using RemoteDesktopClient.Core.Device;
using RemoteDesktopClient.UI.Avalonia.Controls;

namespace RemoteDesktopClient.UI.Avalonia;

public sealed class MainWindow : Window
{
    private IconTextBox _remoteIdInput = null!;
    private ModernButton _remoteConnectButton = null!;

    public MainWindow()
    {
        Title = "RemoteConnect";
        MinWidth = 860;
        MinHeight = 640;
        Width = 1180;
        Height = 840;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = new SolidColorBrush(AppTheme.Background);

        var root = new DockPanel();
        root.Children.Add(BuildHeaderBar());
        root.Children.Add(BuildFooterBar());
        root.Children.Add(BuildContentArea());
        Content = root;

        // Enter anywhere triggers Connect, mirroring Form.AcceptButton's effect in the
        // WinForms version — done here via KeyDown instead, since Avalonia's custom,
        // non-templated ModernButton has no equivalent "default button" concept to hook.
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter && _remoteConnectButton.IsEnabled)
                _remoteConnectButton.PerformClick();
        };
    }

    private static Control BuildHeaderBar()
    {
        var header = new Border
        {
            [DockPanel.DockProperty] = Dock.Top,
            Height = 84,
            Background = new SolidColorBrush(AppTheme.CardBackground),
            BorderBrush = new SolidColorBrush(AppTheme.CardBorder),
            BorderThickness = new Thickness(0, 0, 0, 1),
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        grid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

        var logoMark = new LogoMark { Width = 40, Height = 40, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(24, 0, 12, 0) };
        Grid.SetColumn(logoMark, 0);

        var titleStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 0, 0) };
        titleStack.Children.Add(TextBlockOf("RemoteConnect", AppTheme.FontTitle, AppTheme.TextPrimary));
        titleStack.Children.Add(TextBlockOf("Securely connect to your devices, anytime.", AppTheme.FontSubtitle, AppTheme.TextSecondary, new Thickness(0, 2, 0, 0)));
        Grid.SetColumn(titleStack, 1);

        var rightStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 24, 0), Spacing = 20 };
        var avatar = new AvatarBadge { Width = 56, Height = 40 };
        var settingsButton = new ModernButton { Text = "Settings", Icon = IconKind.Gear, Variant = ButtonVariant.Subtle, Width = 112, Height = 40 };
        rightStack.Children.Add(avatar);
        rightStack.Children.Add(settingsButton);
        Grid.SetColumn(rightStack, 2);

        grid.Children.Add(logoMark);
        grid.Children.Add(titleStack);
        grid.Children.Add(rightStack);
        header.Child = grid;
        return header;
    }

    private static Control BuildFooterBar()
    {
        var footer = new Border
        {
            [DockPanel.DockProperty] = Dock.Bottom,
            Height = 40,
            Background = new SolidColorBrush(AppTheme.CardBackground),
            BorderBrush = new SolidColorBrush(AppTheme.CardBorder),
            BorderThickness = new Thickness(0, 1, 0, 0),
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        grid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

        var statusStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(24, 0, 0, 0), Spacing = 8 };
        statusStack.Children.Add(new StatusDot { Width = 7, Height = 7, VerticalAlignment = VerticalAlignment.Center });
        statusStack.Children.Add(TextBlockOf("Ready to connect   ·   Secure   ·   Reliable   ·   Fast", AppTheme.FontSmall, AppTheme.TextSecondary));
        Grid.SetColumn(statusStack, 0);

        var versionLabel = TextBlockOf("RemoteConnect v1.0.0", AppTheme.FontSmall, AppTheme.TextSecondary);
        versionLabel.VerticalAlignment = VerticalAlignment.Center;
        versionLabel.Margin = new Thickness(0, 0, 24, 0);
        Grid.SetColumn(versionLabel, 2);

        grid.Children.Add(statusStack);
        grid.Children.Add(versionLabel);
        footer.Child = grid;
        return footer;
    }

    // Content: This Device (hero, fixed) + Remote Connection (fixed) + Recent
    // Connections (fills whatever vertical space remains, and scrolls
    // internally past that). MaxWidth + Center on the outer Grid caps and
    // centers the column on wide windows without any manual resize handling —
    // Avalonia's layout system does this declaratively, unlike WinForms'
    // TableLayoutPanel, which needed a hand-written Reflow() on every resize.
    // (HorizontalAlignment.Stretch does NOT do this: a Stretch child capped by
    // MaxWidth below the space it's given renders at the start of that space,
    // not centered within it — verified by rendering at 1800px width.)
    private Control BuildContentArea()
    {
        var layout = new Grid
        {
            MaxWidth = 1400,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(28, 20, 28, 16),
        };
        // Each row is the card's intended content height PLUS its own bottom
        // margin (200+20, 178+20) — matching the same content+margin sizing
        // used in the WinForms version, since Margin still subtracts from a
        // fixed-size row's available space in Avalonia's layout system too.
        layout.RowDefinitions.Add(new RowDefinition(220, GridUnitType.Pixel));
        layout.RowDefinitions.Add(new RowDefinition(198, GridUnitType.Pixel));
        layout.RowDefinitions.Add(new RowDefinition(1, GridUnitType.Star));

        var thisDeviceCard = BuildThisDeviceCard();
        Grid.SetRow(thisDeviceCard, 0);
        thisDeviceCard.Margin = new Thickness(0, 0, 0, 20);

        var remoteConnectionCard = BuildRemoteConnectionCard();
        Grid.SetRow(remoteConnectionCard, 1);
        remoteConnectionCard.Margin = new Thickness(0, 0, 0, 20);

        var recentConnectionsCard = BuildRecentConnectionsCard(CreateMockRecentConnections());
        Grid.SetRow(recentConnectionsCard, 2);

        layout.Children.Add(thisDeviceCard);
        layout.Children.Add(remoteConnectionCard);
        layout.Children.Add(recentConnectionsCard);

        return layout;
    }

    private static Border BuildThisDeviceCard()
    {
        var card = Card(AppTheme.HeroBackground, AppTheme.HeroBackground);

        var halves = new Grid();
        halves.ColumnDefinitions.Add(new ColumnDefinition(54, GridUnitType.Star));
        halves.ColumnDefinitions.Add(new ColumnDefinition(46, GridUnitType.Star));

        var identity = BuildIdentityHalf();
        Grid.SetColumn(identity, 0);
        var readyToConnect = BuildReadyToConnectHalf();
        Grid.SetColumn(readyToConnect, 1);

        halves.Children.Add(identity);
        halves.Children.Add(readyToConnect);
        card.Child = halves;
        return card;
    }

    private static Control BuildIdentityHalf()
    {
        var grid = new Grid { Margin = new Thickness(0, 6, 0, 6) };
        grid.ColumnDefinitions.Add(new ColumnDefinition(96, GridUnitType.Pixel));
        grid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));

        var iconBadge = new IconBadge
        {
            Icon = IconKind.Monitor,
            TintBackground = AppTheme.CardBackground,
            TintForeground = AppTheme.Accent,
            Width = 72,
            Height = 72,
            CornerRadius = 16,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(iconBadge, 0);

        var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        textStack.Children.Add(TextBlockOf("This Device", AppTheme.FontBody, AppTheme.TextSecondary, new Thickness(0, 0, 0, 4)));
        textStack.Children.Add(TextBlockOf("DESKTOP-7XQ2KD1", AppTheme.FontTitle, AppTheme.TextPrimary, new Thickness(0, 0, 0, 12)));
        var idChip = new IdChip { DeviceId = "482 913 607", HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 12) };
        textStack.Children.Add(idChip);
        var statusBadge = new StatusBadge { IsOnline = true, HorizontalAlignment = HorizontalAlignment.Left }; // placeholder — "online" once signaling exists
        textStack.Children.Add(statusBadge);
        Grid.SetColumn(textStack, 1);

        grid.Children.Add(iconBadge);
        grid.Children.Add(textStack);
        return grid;
    }

    private static Control BuildReadyToConnectHalf()
    {
        var grid = new Grid { Margin = new Thickness(0, 6, 0, 6) };
        grid.ColumnDefinitions.Add(new ColumnDefinition(150, GridUnitType.Pixel));
        grid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));

        var illustration = new HeroIllustration { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(illustration, 0);

        var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        textStack.Children.Add(TextBlockOf("Ready to connect", AppTheme.FontSectionHeader, AppTheme.TextPrimary, new Thickness(0, 0, 0, 8)));
        var description = TextBlockOf(
            "Share this ID with someone to allow them to connect to this device.",
            AppTheme.FontBody, AppTheme.TextSecondary);
        description.TextWrapping = TextWrapping.Wrap;
        description.Height = 48;
        textStack.Children.Add(description);
        Grid.SetColumn(textStack, 1);

        grid.Children.Add(illustration);
        grid.Children.Add(textStack);
        return grid;
    }

    private Border BuildRemoteConnectionCard()
    {
        var card = Card(AppTheme.CardBackground, AppTheme.CardBorder);

        var layout = new Grid();
        layout.RowDefinitions.Add(new RowDefinition(58, GridUnitType.Pixel));
        layout.RowDefinitions.Add(new RowDefinition(1, GridUnitType.Star));

        // Header: icon | title+subtitle stack, vertically centered as a group
        // via the StackPanel's own VerticalAlignment — Avalonia sizes the
        // stack to its two lines of text directly, no AutoSize-vs-Dock.Fill
        // workaround needed here (see the WinForms version's headerTextStack
        // comment for the bug that required one there).
        var headerRow = new Grid();
        headerRow.ColumnDefinitions.Add(new ColumnDefinition(48, GridUnitType.Pixel));
        headerRow.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
        Grid.SetRow(headerRow, 0);

        var iconBadge = new IconBadge
        {
            Icon = IconKind.Link,
            TintBackground = AppTheme.IconBlueBg,
            TintForeground = AppTheme.IconBlueFg,
            Width = 36,
            Height = 36,
            CornerRadius = 10,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(iconBadge, 0);

        var headerTextStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        headerTextStack.Children.Add(TextBlockOf("Connect to a Remote Device", AppTheme.FontSectionHeader, AppTheme.TextPrimary, new Thickness(0, 0, 0, 4)));
        headerTextStack.Children.Add(TextBlockOf("Enter the remote device's ID to establish a connection.", AppTheme.FontBody, AppTheme.TextSecondary));
        Grid.SetColumn(headerTextStack, 1);

        headerRow.Children.Add(iconBadge);
        headerRow.Children.Add(headerTextStack);

        // Input row: the ID box fills all remaining width, the Connect button
        // keeps a comfortable fixed width, with a clear gap via the input's
        // own right Margin.
        var inputRow = new Grid { VerticalAlignment = VerticalAlignment.Center };
        inputRow.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
        inputRow.ColumnDefinitions.Add(new ColumnDefinition(160, GridUnitType.Pixel));
        Grid.SetRow(inputRow, 1);

        _remoteIdInput = new IconTextBox
        {
            Icon = IconKind.Monitor,
            PlaceholderText = "Enter device ID (e.g. 123 456 789)",
            Margin = new Thickness(0, 4, 24, 4),
        };
        Grid.SetColumn(_remoteIdInput, 0);

        _remoteConnectButton = new ModernButton
        {
            Text = "Connect",
            Icon = IconKind.Send,
            Variant = ButtonVariant.Primary,
            Width = 160,
            Height = 52,
            IsEnabled = false,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(_remoteConnectButton, 1);
        _remoteIdInput.TextChanged += (_, _) =>
            _remoteConnectButton.IsEnabled = !string.IsNullOrWhiteSpace(_remoteIdInput.Text);

        inputRow.Children.Add(_remoteIdInput);
        inputRow.Children.Add(_remoteConnectButton);

        layout.Children.Add(headerRow);
        layout.Children.Add(inputRow);

        card.Child = layout;
        return card;
    }

    private static Border BuildRecentConnectionsCard(IReadOnlyList<DeviceConnection> devices)
    {
        var card = Card(AppTheme.CardBackground, AppTheme.CardBorder);

        var layout = new Grid();
        layout.RowDefinitions.Add(new RowDefinition(58, GridUnitType.Pixel));
        layout.RowDefinitions.Add(new RowDefinition(1, GridUnitType.Star));

        var headerRow = new Grid();
        headerRow.ColumnDefinitions.Add(new ColumnDefinition(48, GridUnitType.Pixel));
        headerRow.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
        headerRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        Grid.SetRow(headerRow, 0);

        var iconBadge = new IconBadge
        {
            Icon = IconKind.Clock,
            TintBackground = AppTheme.IconBlueBg,
            TintForeground = AppTheme.IconBlueFg,
            Width = 36,
            Height = 36,
            CornerRadius = 10,
            VerticalAlignment = VerticalAlignment.Top,
        };
        Grid.SetColumn(iconBadge, 0);

        var textStack = new StackPanel();
        textStack.Children.Add(TextBlockOf("Recent Connections", AppTheme.FontSectionHeader, AppTheme.TextPrimary));
        textStack.Children.Add(TextBlockOf("Quickly connect to your frequently used devices.", AppTheme.FontBody, AppTheme.TextSecondary, new Thickness(0, 4, 0, 0)));
        Grid.SetColumn(textStack, 1);

        var clearAllButton = new ModernButton { Text = "Clear All", Icon = IconKind.Trash, Variant = ButtonVariant.Subtle, Width = 112, Height = 38, VerticalAlignment = VerticalAlignment.Top };
        Grid.SetColumn(clearAllButton, 2);

        headerRow.Children.Add(iconBadge);
        headerRow.Children.Add(textStack);
        headerRow.Children.Add(clearAllButton);

        var cardsFlow = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var device in devices)
        {
            var (icon, background, foreground) = GetIconStyle(device.Type);
            cardsFlow.Children.Add(new RecentDeviceCard(device, icon, background, foreground));
        }

        var scrollViewer = new ScrollViewer
        {
            Content = cardsFlow,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
        Grid.SetRow(scrollViewer, 1);

        layout.Children.Add(headerRow);
        layout.Children.Add(scrollViewer);

        card.Child = layout;
        return card;
    }

    // --- small shared builders -------------------------------------------

    private static Border Card(Color background, Color border) => new()
    {
        Background = new SolidColorBrush(background),
        BorderBrush = new SolidColorBrush(border),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(12),
        Padding = new Thickness(24),
    };

    private static TextBlock TextBlockOf(string text, AppFont font, Color color, Thickness? margin = null) => new()
    {
        Text = text,
        FontFamily = font.Family,
        FontSize = font.Size,
        FontWeight = font.Weight,
        FontStyle = font.Style,
        Foreground = new SolidColorBrush(color),
        Margin = margin ?? default,
    };

    // DeviceConnection (Core) only knows its DeviceType, not how that type is
    // drawn — the icon glyph and tint colors are a UI concern, resolved here.
    private static (IconKind Icon, Color Background, Color Foreground) GetIconStyle(DeviceType type) => type switch
    {
        DeviceType.Desktop => (IconKind.Monitor, AppTheme.IconBlueBg, AppTheme.IconBlueFg),
        DeviceType.Home => (IconKind.Home, AppTheme.IconPurpleBg, AppTheme.IconPurpleFg),
        DeviceType.Laptop => (IconKind.Laptop, AppTheme.IconGreenBg, AppTheme.IconGreenFg),
        DeviceType.Server => (IconKind.Server, AppTheme.IconOrangeBg, AppTheme.IconOrangeFg),
        _ => (IconKind.Monitor, AppTheme.IconBlueBg, AppTheme.IconBlueFg),
    };

    private static IReadOnlyList<DeviceConnection> CreateMockRecentConnections() => new List<DeviceConnection>
    {
        new()
        {
            DeviceName = "Office Desktop — Marketing Team",
            DeviceId = "118 204 552",
            Status = ConnectionStatus.Online,
            LastConnectedText = "Last connected: 2 hours ago",
            Type = DeviceType.Desktop,
        },
        new()
        {
            DeviceName = "Home PC",
            DeviceId = "902 447 310",
            Status = ConnectionStatus.Offline,
            LastConnectedText = "Last connected: 1 day ago",
            Type = DeviceType.Home,
        },
        new()
        {
            DeviceName = "Laptop — Sarah",
            DeviceId = "573 810 226",
            Status = ConnectionStatus.Online,
            LastConnectedText = "Last connected: 3 hours ago",
            Type = DeviceType.Laptop,
        },
        new()
        {
            DeviceName = "Server Room Workstation 4",
            DeviceId = "301 665 918",
            Status = ConnectionStatus.Offline,
            LastConnectedText = "Last connected: 5 days ago",
            Type = DeviceType.Server,
        },
    };

    /// <summary>The blue rounded-square app logo with a centered white dot, top-left of the header.</summary>
    private sealed class LogoMark : Control
    {
        public override void Render(DrawingContext context)
        {
            var rect = new Rect(0, 0, Bounds.Width, Bounds.Height);
            context.DrawRectangle(new SolidColorBrush(AppTheme.Accent), null, new RoundedRect(rect, 11));
            context.DrawEllipse(Brushes.White, null, rect.Center, 5, 5);
        }
    }

    /// <summary>The circular "M" avatar with a chevron, top-right of the header.</summary>
    private sealed class AvatarBadge : Control
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

    /// <summary>The small green "online" dot in the footer.</summary>
    private sealed class StatusDot : Control
    {
        public override void Render(DrawingContext context) =>
            context.DrawEllipse(new SolidColorBrush(AppTheme.Online), null, new Rect(0, 0, Bounds.Width, Bounds.Height).Center, Bounds.Width / 2, Bounds.Height / 2);
    }
}
