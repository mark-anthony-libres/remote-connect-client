using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using RemoteDesktopClient.UI.Avalonia;
using RemoteDesktopClient.UI.Avalonia.Components.Shared;

namespace RemoteDesktopClient.UI.Avalonia.Components.FooterBar;

internal sealed class FooterBar : Border
{
    public FooterBar()
    {
        this[DockPanel.DockProperty] = Dock.Bottom;
        Height = 40;
        Background = new SolidColorBrush(AppTheme.CardBackground);
        BorderBrush = new SolidColorBrush(AppTheme.CardBorder);
        BorderThickness = new Thickness(0, 1, 0, 0);

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        grid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

        var statusStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(24, 0, 0, 0), Spacing = 8 };
        statusStack.Children.Add(new StatusDot { Width = 7, Height = 7, VerticalAlignment = VerticalAlignment.Center });
        statusStack.Children.Add(SharedBuilders.TextBlockOf("Ready to connect   ·   Secure   ·   Reliable   ·   Fast", AppTheme.FontSmall, AppTheme.TextSecondary));
        Grid.SetColumn(statusStack, 0);

        var versionLabel = SharedBuilders.TextBlockOf("RemoteConnect v1.0.0", AppTheme.FontSmall, AppTheme.TextSecondary);
        versionLabel.VerticalAlignment = VerticalAlignment.Center;
        versionLabel.Margin = new Thickness(0, 0, 24, 0);
        Grid.SetColumn(versionLabel, 2);

        grid.Children.Add(statusStack);
        grid.Children.Add(versionLabel);
        Child = grid;
    }
}
