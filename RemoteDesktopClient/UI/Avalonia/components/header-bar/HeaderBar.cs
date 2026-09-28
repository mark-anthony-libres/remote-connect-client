using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using RemoteDesktopClient.Services;
using RemoteDesktopClient.UI.Avalonia;
using RemoteDesktopClient.UI.Avalonia.Components.Shared;

namespace RemoteDesktopClient.UI.Avalonia.Components.HeaderBar;

internal sealed class HeaderBar : Border
{
    public HeaderBar()
    {
        this[DockPanel.DockProperty] = Dock.Top;
        Height = 84;
        Background = new SolidColorBrush(AppTheme.CardBackground);
        BorderBrush = new SolidColorBrush(AppTheme.CardBorder);
        BorderThickness = new Thickness(0, 0, 0, 1);

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        grid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

        var logoMark = new LogoMark { Width = 40, Height = 40, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(24, 0, 12, 0) };
        Grid.SetColumn(logoMark, 0);

        var titleStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        titleStack.Children.Add(SharedBuilders.TextBlockOf("RemoteConnect", AppTheme.FontTitle, AppTheme.TextPrimary));
        titleStack.Children.Add(SharedBuilders.TextBlockOf("Securely connect to your devices, anytime.", AppTheme.FontSubtitle, AppTheme.TextSecondary, new Thickness(0, 2, 0, 0)));
        Grid.SetColumn(titleStack, 1);

        var rightStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 24, 0), Spacing = 20 };
        var avatar = new AvatarBadge { Width = 56, Height = 40 };
        var settingsButton = new ModernButton { Text = "Settings", Icon = IconKind.Gear, Variant = ButtonVariant.Subtle, Width = 112, Height = 40 };
        var settingsMenu = BuildSettingsMenu(settingsButton);
        settingsButton.Click += (_, _) => settingsMenu.IsOpen = !settingsMenu.IsOpen;
        rightStack.Children.Add(avatar);
        rightStack.Children.Add(settingsButton);
        rightStack.Children.Add(settingsMenu);
        Grid.SetColumn(rightStack, 2);

        grid.Children.Add(logoMark);
        grid.Children.Add(titleStack);
        grid.Children.Add(rightStack);
        Child = grid;
    }

    private static Popup BuildSettingsMenu(Control placementTarget)
    {
        var renewRow = new Border
        {
            Padding = new Thickness(16, 10),
            Cursor = new Cursor(StandardCursorType.Hand),
            Background = Brushes.Transparent,
        };
        renewRow.Child = SharedBuilders.TextBlockOf("Renew Install Key", AppTheme.FontBody, AppTheme.TextPrimary);
        renewRow.PointerEntered += (_, _) => renewRow.Background = new SolidColorBrush(AppTheme.SubtleButtonHover);
        renewRow.PointerExited += (_, _) => renewRow.Background = Brushes.Transparent;

        var menu = new Popup
        {
            PlacementTarget = placementTarget,
            Placement = PlacementMode.BottomEdgeAlignedRight,
            IsLightDismissEnabled = true,
            Child = new Border
            {
                Background = new SolidColorBrush(AppTheme.CardBackground),
                BorderBrush = new SolidColorBrush(AppTheme.CardBorder),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                MinWidth = 180,
                Child = renewRow,
            },
        };

        renewRow.PointerPressed += (_, _) =>
        {
            DeviceIdentityStore.ClearInstallKey();
            App.Restart();
        };

        return menu;
    }
}
