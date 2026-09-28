using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using RemoteDesktopClient.UI.Avalonia;
using RemoteDesktopClient.UI.Avalonia.Components.Shared;

namespace RemoteDesktopClient.UI.Avalonia.Components.ThisDevicePanel;

internal sealed class ThisDeviceCard : Border
{
    public IdChip IdChip { get; private set; } = null!;
    public StatusBadge StatusBadge { get; private set; } = null!;

    public ThisDeviceCard()
    {
        Background = new SolidColorBrush(AppTheme.HeroBackground);
        BorderBrush = new SolidColorBrush(AppTheme.HeroBackground);
        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(12);
        Padding = new Thickness(24);

        var halves = new Grid();
        halves.ColumnDefinitions.Add(new ColumnDefinition(54, GridUnitType.Star));
        halves.ColumnDefinitions.Add(new ColumnDefinition(46, GridUnitType.Star));

        var identity = BuildIdentityHalf();
        Grid.SetColumn(identity, 0);
        var readyToConnect = BuildReadyToConnectHalf();
        Grid.SetColumn(readyToConnect, 1);

        halves.Children.Add(identity);
        halves.Children.Add(readyToConnect);
        Child = halves;
    }

    private Control BuildIdentityHalf()
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
        textStack.Children.Add(SharedBuilders.TextBlockOf("This Device", AppTheme.FontBody, AppTheme.TextSecondary, new Thickness(0, 0, 0, 4)));
        textStack.Children.Add(SharedBuilders.TextBlockOf("DESKTOP-7XQ2KD1", AppTheme.FontTitle, AppTheme.TextPrimary, new Thickness(0, 0, 0, 12)));
        IdChip = new IdChip { DeviceId = "Connecting…", HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 12) };
        textStack.Children.Add(IdChip);
        StatusBadge = new StatusBadge { IsOnline = false, HorizontalAlignment = HorizontalAlignment.Left };
        textStack.Children.Add(StatusBadge);
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
        textStack.Children.Add(SharedBuilders.TextBlockOf("Ready to connect", AppTheme.FontSectionHeader, AppTheme.TextPrimary, new Thickness(0, 0, 0, 8)));
        var description = SharedBuilders.TextBlockOf(
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
}
